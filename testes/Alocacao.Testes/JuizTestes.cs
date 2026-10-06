using Conde.Alocacao;
using Xunit;

namespace Conde.Alocacao.Testes;

/// <summary>
/// O juiz tambem precisa de juiz.
///
/// A enumeracao de todas as escolhas de buraco e o que permite dizer que uma
/// estrategia DESPERDICOU, em vez de dizer que ela desperdicou mais que a outra.
/// Se ela tivesse um erro, todos os numeros seriam comparacoes contra uma
/// referencia errada.
/// </summary>
public class JuizTestes
{
    private static List<Juiz.Passo> Pedir(params (int Dono, int Tamanho)[] pedidos) =>
        [.. pedidos.Select(p => new Juiz.Passo(p.Dono, p.Tamanho, EhPedido: true))];

    [Fact]
    public void SemPassoNenhumNaoHaRecusa()
    {
        Assert.Equal(0, Juiz.MenosRecusasPossivel(100, []));
    }

    /// <summary>
    /// Quando TUDO cabe, a resposta otima e zero recusas, e toda estrategia
    /// tambem.
    /// </summary>
    [Fact]
    public void QuandoTudoCabeNinguemRecusa()
    {
        var passos = Pedir((1, 30), (2, 30), (3, 30));

        Assert.Equal(0, Juiz.MenosRecusasPossivel(100, passos));

        foreach (var estrategia in Estrategia.Todas())
            Assert.Equal(0, Juiz.RecusasDe(100, estrategia, passos));
    }

    /// <summary>
    /// Quando NADA cabe, nem o otimo escapa: a enumeracao nao inventa memoria.
    /// </summary>
    [Fact]
    public void QuandoNadaCabeAteOOtimoRecusa()
    {
        var passos = Pedir((1, 60), (2, 60), (3, 60));

        Assert.Equal(2, Juiz.MenosRecusasPossivel(100, passos));
    }

    /// <summary>
    /// O CASO QUE SEPARA: uma escolha de buraco salva o pedido seguinte e a outra
    /// nao.
    ///
    /// Memoria de 100, com um buraco de 40 e um de 50 depois de uma alocacao. Um
    /// pedido de 30 cabe nos dois, e so uma das escolhas deixa espaco para um
    /// pedido de 45 depois.
    /// </summary>
    [Fact]
    public void AEscolhaDoBuracoPodeSalvarOPedidoSeguinte()
    {
        var passos = new List<Juiz.Passo>
        {
            new(1, 40, EhPedido: true),
            new(2, 10, EhPedido: true),
            new(3, 50, EhPedido: true),
            new(1, 0, EhPedido: false),
            new(4, 30, EhPedido: true),
            new(3, 0, EhPedido: false),
            new(5, 45, EhPedido: true),
        };

        // o otimo nao recusa nada: poe o 30 no buraco de 40 e deixa o de 50 livre
        Assert.Equal(0, Juiz.MenosRecusasPossivel(100, passos));
    }

    /// <summary>
    /// O OTIMO nunca e pior que qualquer estrategia: ele e o minimo sobre TODAS as
    /// escolhas, e as estrategias sao escolhas.
    ///
    /// Esta e a conferencia que diz que a enumeracao esta mesmo enumerando: se
    /// alguma estrategia batesse o "otimo", ele nao seria otimo.
    /// </summary>
    [Fact]
    public void OOtimoNuncaEhPiorQueQualquerEstrategia()
    {
        for (ulong semente = 1; semente <= 200; semente++)
        {
            var carga = Cargas.Minuscula(10, 1_000, semente);
            var otimo = Juiz.MenosRecusasPossivel(1_000, carga.Passos);

            foreach (var estrategia in Estrategia.Todas())
            {
                var recusas = Juiz.RecusasDe(1_000, estrategia, carga.Passos);

                Assert.True(otimo <= recusas, $"semente {semente}, {estrategia.Nome}: otimo {otimo}, estrategia {recusas}");
            }
        }
    }

    /// <summary>
    /// E o otimo NAO e trivialmente zero: nas sequencias apertadas ele recusa
    /// tambem, e e isso que faz a comparacao valer.
    /// </summary>
    [Fact]
    public void OOtimoTambemRecusa()
    {
        var recusou = 0;

        for (ulong semente = 1; semente <= 200; semente++)
        {
            var carga = Cargas.Minuscula(10, 1_000, semente);

            if (Juiz.MenosRecusasPossivel(1_000, carga.Passos) > 0) recusou++;
        }

        Assert.True(recusou > 100, $"o otimo so recusou em {recusou} de 200 sequencias");
    }

    [Fact]
    public void SequenciaGrandeDemaisParaAEnumeracaoEhRecusada()
    {
        var carga = Cargas.Variada(20, 1_000, 1);

        Assert.Throws<ArgumentException>(() => Juiz.MenosRecusasPossivel(1_000, carga.Passos));
    }

    /// <summary>
    /// As conferencias de contabilidade PEGAM uma memoria estragada: elas nao sao
    /// decorativas.
    /// </summary>
    [Fact]
    public void AsConferenciasPegamUmaMemoriaEstragada()
    {
        var memoria = new Memoria(100);
        memoria.Ocupar(0, 30, dono: 1);
        memoria.Ocupar(1, 30, dono: 2);

        Assert.True(Juiz.AFitaEstaInteira(memoria));
        Assert.True(Juiz.NaoHaBuracosGrudados(memoria));
        Assert.True(Juiz.CadaDonoApareceUmaVez(memoria));

        // devolver sem juntar deixa dois buracos grudados
        memoria.DevolverSemJuntar(1);
        memoria.DevolverSemJuntar(2);

        Assert.True(Juiz.AFitaEstaInteira(memoria));
        Assert.False(Juiz.NaoHaBuracosGrudados(memoria));
    }

    [Fact]
    public void AConferenciaPegaUmDonoRepetido()
    {
        var memoria = new Memoria(100);
        memoria.Ocupar(0, 30, dono: 1);
        memoria.Ocupar(1, 30, dono: 1);

        Assert.False(Juiz.CadaDonoApareceUmaVez(memoria));
    }

    /// <summary>
    /// E a contabilidade do alocador bate com a fita, em toda carga e toda
    /// estrategia.
    /// </summary>
    [Fact]
    public void AContabilidadeDoAlocadorBateComAFita()
    {
        foreach (var carga in Cargas.Todas(50_000))
            foreach (var estrategia in Estrategia.Todas())
            {
                var alocador = new Alocador(50_000, estrategia);

                foreach (var passo in carga.Passos)
                {
                    if (passo.EhPedido) alocador.Pedir(passo.Dono, passo.Tamanho);
                    else alocador.Devolver(passo.Dono);
                }

                Assert.True(Juiz.AContabilidadeBate(alocador), $"{carga.Nome} com {estrategia.Nome}");
                Assert.Equal(alocador.Vivos.Values.Sum(), alocador.Memoria.Usada);
            }
    }

    /// <summary>
    /// As CARGAS sao deterministas, e a uniforme tem mesmo todos os pedidos do
    /// mesmo tamanho.
    /// </summary>
    [Fact]
    public void AsCargasSaoDeterministasEFazemOQueONomeDiz()
    {
        Assert.Equal(Cargas.Variada(500, 10_000).Passos, Cargas.Variada(500, 10_000).Passos);

        var uniforme = Cargas.Uniforme(500, 10_000);
        var tamanhos = uniforme.Passos.Where(p => p.EhPedido).Select(p => p.Tamanho).Distinct().ToList();

        Assert.Single(tamanhos);

        var cauda = Cargas.CaudaLonga(2_000, 100_000).Passos.Where(p => p.EhPedido).Select(p => p.Tamanho).ToList();

        Assert.True(cauda.Max() > 20 * cauda.OrderBy(t => t).ElementAt(cauda.Count / 2));
    }
}
