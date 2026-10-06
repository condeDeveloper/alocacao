using Conde.Alocacao;
using Xunit;

namespace Conde.Alocacao.Testes;

/// <summary>
/// Os testes das TABELAS que o medidor imprime.
///
/// Eles existem porque numero em README que ninguem confere envelhece errado em
/// silencio: alguem troca uma constante, a tabela muda e o texto continua dizendo
/// a frase antiga. Aqui o teste afirma sobre o mesmo objeto que vira linha
/// impressa.
/// </summary>
public class MedidasTestes
{
    [Fact]
    public void ATabelaDeEstrategiasTemUmaLinhaPorParDeCargaEEstrategia()
    {
        var linhas = Medidas.Estrategias();

        Assert.Equal(16, linhas.Count);
        Assert.Equal(4, linhas.Select(l => l.Carga).Distinct().Count());
        Assert.Equal(4, linhas.Select(l => l.Estrategia).Distinct().Count());
    }

    /// <summary>
    /// A frase do texto: na carga uniforme ninguem recusa nada, e e por isso que
    /// medir alocacao com pedidos de tamanho fixo nao descobre nada.
    /// </summary>
    [Fact]
    public void NaCargaUniformeNinguemRecusa()
    {
        foreach (var l in Medidas.Estrategias().Where(l => l.Carga == "uniforme"))
            Assert.Equal(0, l.Recusados);
    }

    /// <summary>
    /// A frase do texto: o PIOR encaixe e mesmo o pior, nas duas cargas com
    /// variacao de tamanho.
    /// </summary>
    [Fact]
    public void OPiorEncaixeEhMesmoOPior()
    {
        var linhas = Medidas.Estrategias();

        foreach (var carga in new[] { "variada", "cauda longa" })
        {
            var grupo = linhas.Where(l => l.Carga == carga).ToList();
            var pior = grupo.MaxBy(l => l.Recusados)!;

            Assert.Equal("pior encaixe", pior.Estrategia);
        }
    }

    /// <summary>
    /// E ele tambem OLHA MAIS buracos por pedido: ele e pior nas duas colunas ao
    /// mesmo tempo.
    ///
    /// A carga alternada fica de fora de proposito: nela todos os pedidos acontecem
    /// antes de qualquer devolucao, entao nunca ha mais de um buraco na hora de
    /// escolher e as quatro estrategias olham um so. Afirmar o contrario dela seria
    /// afirmar sobre uma coluna que nao tem o que medir.
    /// </summary>
    [Fact]
    public void OPiorEncaixeTambemOlhaMaisBuracos()
    {
        var linhas = Medidas.Estrategias();

        foreach (var grupo in linhas.GroupBy(l => l.Carga).Where(g => g.Key != "alternada"))
        {
            var pior = grupo.Single(l => l.Estrategia == "pior encaixe");
            var proximo = grupo.Single(l => l.Estrategia == "proximo encaixe");

            Assert.True(pior.OlhadosPorPedido > proximo.OlhadosPorPedido, grupo.Key);
        }

        // e na alternada as quatro olham praticamente o mesmo
        var alternada = linhas.Where(l => l.Carga == "alternada").ToList();
        Assert.True(alternada.Max(l => l.OlhadosPorPedido) < 3);
    }

    /// <summary>
    /// A frase do texto: o MELHOR encaixe nao e o melhor na carga de cauda longa.
    /// </summary>
    [Fact]
    public void OMelhorEncaixeNaoEhOMelhorNaCaudaLonga()
    {
        var grupo = Medidas.Estrategias().Where(l => l.Carga == "cauda longa").ToList();

        var primeiro = grupo.Single(l => l.Estrategia == "primeiro encaixe");
        var melhor = grupo.Single(l => l.Estrategia == "melhor encaixe");

        Assert.True(melhor.Recusados >= primeiro.Recusados,
            $"primeiro {primeiro.Recusados}, melhor {melhor.Recusados}");
    }

    /// <summary>
    /// A frase do texto: o JUIZ mostra que a escolha do buraco quase nao muda nada
    /// em sequencias curtas.
    ///
    /// Essa e a conclusao honesta e ela nao e a esperada: a enumeracao de todas as
    /// escolhas possiveis bate as estrategias por centesimos de recusa. O que
    /// importa de verdade nao e qual buraco escolher, e a juncao e o tamanho dos
    /// pedidos.
    /// </summary>
    [Fact]
    public void OJuizMostraQueAEscolhaDoBuracoQuaseNaoMuda()
    {
        var linhas = Medidas.Juiz(50);

        Assert.Equal(3, linhas.Count);

        foreach (var l in linhas)
        {
            Assert.True(l.Otimo <= l.Primeiro + 1e-9);
            Assert.True(l.Otimo <= l.Melhor + 1e-9);
            Assert.True(l.Otimo <= l.Pior + 1e-9);

            Assert.True(l.Primeiro - l.Otimo < 0.15, $"{l.Passos} passos: otimo {l.Otimo:F2}, primeiro {l.Primeiro:F2}");
        }

        // e o otimo cresce com o tamanho da sequencia
        for (var i = 1; i < linhas.Count; i++) Assert.True(linhas[i].Otimo > linhas[i - 1].Otimo);
    }

    /// <summary>
    /// A frase do texto: sem a JUNCAO, a memoria vira po.
    /// </summary>
    [Fact]
    public void SemAJuncaoAMemoriaViraPo()
    {
        var linhas = Medidas.Juncoes();

        Assert.Equal(2, linhas.Count);

        var com = linhas.Single(l => l.Jeito == "com juncao");
        var sem = linhas.Single(l => l.Jeito == "sem juncao");

        Assert.True(sem.Buracos > 5 * com.Buracos, $"com {com.Buracos}, sem {sem.Buracos}");
        Assert.True(sem.Recusados > com.Recusados, $"com {com.Recusados}, sem {sem.Recusados}");
    }

    /// <summary>
    /// A frase do texto: o sistema de companheiros nao desperdica nada com pedidos
    /// que sao potencia de dois, e desperdica um quarto com pedidos sorteados.
    /// </summary>
    [Fact]
    public void OBuddyDesperdicaSoOArredondamento()
    {
        var linhas = Medidas.Buddies(18);

        Assert.Equal(4, linhas.Count);
        Assert.All(linhas, l => Assert.InRange(l.Desperdicio, 0.0, 1.0));
        Assert.All(linhas, l => Assert.True(l.Ocupado >= l.Pedido));

        var uniforme = linhas.Single(l => l.Carga == "uniforme");
        Assert.Equal(0.0, uniforme.Desperdicio, 9);

        Assert.Contains(linhas, l => l.Desperdicio > 0.2);
    }

    /// <summary>
    /// A frase do texto: o pior caso do arredondamento e quase o dobro, e ele
    /// acontece sempre que o pedido passa de uma potencia de dois por um byte.
    /// </summary>
    [Fact]
    public void OPiorArredondamentoEhQuaseODobro()
    {
        var linhas = Medidas.Arredondamentos();

        Assert.Equal(9, linhas.Count);

        foreach (var l in linhas)
        {
            Assert.True(l.Ocupado >= l.Pedido);
            Assert.InRange(l.Desperdicio, 0.0, 0.5);
        }

        // as potencias de dois nao desperdicam nada
        foreach (var l in linhas.Where(l => (l.Pedido & (l.Pedido - 1)) == 0))
            Assert.Equal(0.0, l.Desperdicio, 9);

        // e o pior chega perto de metade do bloco
        Assert.True(linhas.Max(l => l.Desperdicio) >= 0.48);
    }
}
