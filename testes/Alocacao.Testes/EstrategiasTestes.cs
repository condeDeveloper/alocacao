using Conde.Alocacao;
using Xunit;

namespace Conde.Alocacao.Testes;

public class EstrategiasTestes
{
    /// <summary>
    /// Uma memoria com tres buracos de tamanhos diferentes, montada a mao, para as
    /// quatro estrategias escolherem coisas diferentes.
    /// </summary>
    private static Memoria ComTresBuracos()
    {
        // buracos de 50, 20 e 100, nessa ordem
        var memoria = new Memoria(200);

        memoria.Ocupar(0, 50, dono: Bloco.Livre - 1);
        memoria.Ocupar(1, 10, dono: 1);
        memoria.Ocupar(2, 20, dono: Bloco.Livre - 2);
        memoria.Ocupar(3, 20, dono: 2);

        memoria.DevolverSemJuntar(Bloco.Livre - 1);
        memoria.DevolverSemJuntar(Bloco.Livre - 2);

        return memoria;
    }

    [Fact]
    public void CadaEstrategiaEscolheOBuracoQueONomeDiz()
    {
        var memoria = ComTresBuracos();
        var buracos = memoria.Buracos().ToList();

        Assert.Equal(3, buracos.Count);
        Assert.Equal([50, 20, 100], buracos.Select(b => b.Bloco.Tamanho));

        Assert.Equal(buracos[0].Indice, Estrategia.PrimeiroEncaixe().Escolher(memoria, 10));
        Assert.Equal(buracos[1].Indice, Estrategia.MelhorEncaixe().Escolher(memoria, 10));
        Assert.Equal(buracos[2].Indice, Estrategia.PiorEncaixe().Escolher(memoria, 10));
    }

    [Fact]
    public void NenhumaEstrategiaEscolheUmBuracoQueNaoCabe()
    {
        var memoria = ComTresBuracos();

        foreach (var estrategia in Estrategia.Todas())
        {
            var indice = estrategia.Escolher(memoria, 60);

            Assert.True(indice >= 0);
            Assert.True(memoria.Blocos[indice].Tamanho >= 60);
            Assert.True(memoria.Blocos[indice].EstaLivre);
        }
    }

    [Fact]
    public void QuandoNadaCabeTodasRecusam()
    {
        var memoria = ComTresBuracos();

        foreach (var estrategia in Estrategia.Todas())
            Assert.Equal(-1, estrategia.Escolher(memoria, 500));
    }

    /// <summary>
    /// O PROXIMO ENCAIXE lembra onde parou: ele espalha os pedidos pela memoria em
    /// vez de amontoar no comeco.
    /// </summary>
    [Fact]
    public void OProximoEncaixeLembraOndeParou()
    {
        var estrategia = Estrategia.ProximoEncaixe();
        var memoria = new Memoria(300);

        estrategia.Recomecar();

        // o primeiro pedido vai para o comeco
        Assert.Equal(0, estrategia.Escolher(memoria, 50));
        memoria.Ocupar(0, 50, dono: 1);

        // o segundo continua de onde parou, que agora e o buraco depois dele
        var indice = estrategia.Escolher(memoria, 50);
        Assert.Equal(1, indice);
    }

    /// <summary>
    /// E ele OLHA MENOS buracos por pedido que os outros tres, que e a razao de ele
    /// existir.
    /// </summary>
    [Fact]
    public void OProximoEncaixeOlhaMenosBuracos()
    {
        var carga = Cargas.CaudaLonga(4_000, 100_000);
        var custos = new Dictionary<string, double>();

        foreach (var estrategia in Estrategia.Todas())
        {
            var alocador = new Alocador(100_000, estrategia);

            foreach (var passo in carga.Passos)
            {
                if (passo.EhPedido) alocador.Pedir(passo.Dono, passo.Tamanho);
                else alocador.Devolver(passo.Dono);
            }

            custos[estrategia.Nome] = (double)alocador.BuracosOlhados / alocador.Pedidos;
        }

        Assert.True(custos["proximo encaixe"] < custos["primeiro encaixe"]);
        Assert.True(custos["proximo encaixe"] < custos["melhor encaixe"]);
    }

    /// <summary>
    /// O ACHADO QUE O NOME ESCONDE: o MELHOR encaixe nao e o melhor.
    ///
    /// Escolher o buraco mais justo deixa a sobra mais pequena possivel, e uma
    /// sobra pequena demais nao serve para mais nada: ela vira uma lasca que fica
    /// ocupando lugar na lista para sempre. Na carga de cauda longa ele recusa mais
    /// pedidos que o primeiro encaixe, que e o mais burro dos quatro.
    /// </summary>
    [Fact]
    public void OMelhorEncaixeNaoEhOMelhor()
    {
        var carga = Cargas.CaudaLonga(4_000, 100_000);

        var primeiro = Juiz.RecusasDe(100_000, Estrategia.PrimeiroEncaixe(), carga.Passos);
        var melhor = Juiz.RecusasDe(100_000, Estrategia.MelhorEncaixe(), carga.Passos);

        Assert.True(melhor >= primeiro, $"primeiro {primeiro}, melhor {melhor}");
    }

    /// <summary>
    /// E o PIOR encaixe e mesmo o pior: a ideia de deixar a sobra grande destroi os
    /// buracos grandes.
    /// </summary>
    [Fact]
    public void OPiorEncaixeEhMesmoOPior()
    {
        foreach (var carga in new[] { Cargas.Variada(4_000, 100_000), Cargas.CaudaLonga(4_000, 100_000) })
        {
            var pior = Juiz.RecusasDe(100_000, Estrategia.PiorEncaixe(), carga.Passos);
            var primeiro = Juiz.RecusasDe(100_000, Estrategia.PrimeiroEncaixe(), carga.Passos);
            var melhor = Juiz.RecusasDe(100_000, Estrategia.MelhorEncaixe(), carga.Passos);

            Assert.True(pior > primeiro, $"{carga.Nome}: pior {pior}, primeiro {primeiro}");
            Assert.True(pior > melhor, $"{carga.Nome}: pior {pior}, melhor {melhor}");
        }
    }

    /// <summary>
    /// E com pedidos TODOS DO MESMO TAMANHO nenhuma estrategia fragmenta: toda
    /// sobra cabe o proximo pedido exatamente.
    ///
    /// E por isso que medir alocacao com pedidos de tamanho fixo nao descobre nada.
    /// </summary>
    [Fact]
    public void ComPedidosDoMesmoTamanhoNinguemFragmenta()
    {
        var carga = Cargas.Uniforme(4_000, 100_000);

        foreach (var estrategia in Estrategia.Todas())
            Assert.Equal(0, Juiz.RecusasDe(100_000, estrategia, carga.Passos));
    }

    /// <summary>
    /// A FRAGMENTACAO EXTERNA, no retrato: o pedido final cabe na memoria livre
    /// somada e nao cabe em nenhum buraco.
    ///
    /// E a definicao do problema, e ela e uma frase: ter espaco e nao ter espaco
    /// CONTINUO.
    /// </summary>
    [Fact]
    public void AFragmentacaoExternaEhTerEspacoENaoTerEspacoContinuo()
    {
        const int memoria = 100_000;
        var carga = Cargas.Alternada(memoria / 400, memoria);

        var alocador = new Alocador(memoria, Estrategia.PrimeiroEncaixe());

        // tudo menos o ultimo pedido
        foreach (var passo in carga.Passos.Take(carga.Passos.Count - 1))
        {
            if (passo.EhPedido) alocador.Pedir(passo.Dono, passo.Tamanho);
            else alocador.Devolver(passo.Dono);
        }

        var ultimo = carga.Passos[^1];

        Assert.True(alocador.Memoria.Livre >= ultimo.Tamanho, "nao sobrou memoria livre suficiente");
        Assert.True(alocador.Memoria.MaiorBuraco < ultimo.Tamanho, "algum buraco ainda cabia o pedido");
        Assert.False(alocador.Pedir(ultimo.Dono, ultimo.Tamanho));
    }

    [Fact]
    public void CadaEstrategiaTemNomeESeImprimePeloNome()
    {
        foreach (var estrategia in Estrategia.Todas())
        {
            Assert.False(string.IsNullOrWhiteSpace(estrategia.Nome));
            Assert.Equal(estrategia.Nome, estrategia.ToString());
        }

        Assert.Equal(4, Estrategia.Todas().Count());
    }
}
