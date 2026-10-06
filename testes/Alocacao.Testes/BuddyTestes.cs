using Conde.Alocacao;
using Xunit;

namespace Conde.Alocacao.Testes;

public class BuddyTestes
{
    /// <summary>
    /// A CONTABILIDADE fecha depois de cada operacao: a soma do livre com o ocupado
    /// e a memoria inteira, e nenhum bloco se sobrepoe a outro.
    ///
    /// Esta e a conferencia que torna todo o resto confiavel, e ela e feita byte a
    /// byte, marcando quem ocupa cada endereco.
    /// </summary>
    [Fact]
    public void AContabilidadeFechaDepoisDeCadaOperacao()
    {
        foreach (var carga in Cargas.Todas(1 << 14))
        {
            var buddy = new Buddy(14);

            foreach (var passo in carga.Passos)
            {
                if (passo.EhPedido) buddy.Pedir(passo.Dono, passo.Tamanho);
                else buddy.Devolver(passo.Dono);

                Assert.True(buddy.ContabilidadeFecha(), carga.Nome);
                Assert.True(buddy.TodoBlocoEstaAlinhado(), carga.Nome);
            }
        }
    }

    /// <summary>
    /// O COMPANHEIRO de um bloco e um OU-EXCLUSIVO com o tamanho, e so.
    ///
    /// Essa conta e a razao inteira de o sistema existir: ela torna a juncao
    /// imediata, sem procurar nada numa lista.
    /// </summary>
    [Fact]
    public void OCompanheiroEhUmOuExclusivo()
    {
        Assert.Equal(16, Buddy.Companheiro(0, 4));
        Assert.Equal(0, Buddy.Companheiro(16, 4));
        Assert.Equal(40, Buddy.Companheiro(32, 3));
        Assert.Equal(32, Buddy.Companheiro(40, 3));

        // e o companheiro do companheiro e o original
        for (var nivel = 0; nivel < 10; nivel++)
            for (var endereco = 0; endereco < 1024; endereco += 1 << nivel)
                Assert.Equal(endereco, Buddy.Companheiro(Buddy.Companheiro(endereco, nivel), nivel));
    }

    /// <summary>
    /// O ARREDONDAMENTO para a potencia de dois seguinte: um pedido de 33 ocupa 64.
    ///
    /// E aqui que a fragmentacao interna nasce, e nada depois disso desfaz esse
    /// arredondamento.
    /// </summary>
    [Fact]
    public void OArredondamentoVaiParaAPotenciaSeguinte()
    {
        Assert.Equal(0, Buddy.NivelPara(1));
        Assert.Equal(3, Buddy.NivelPara(8));
        Assert.Equal(4, Buddy.NivelPara(9));
        Assert.Equal(5, Buddy.NivelPara(32));
        Assert.Equal(6, Buddy.NivelPara(33));
        Assert.Equal(6, Buddy.NivelPara(64));
        Assert.Equal(7, Buddy.NivelPara(65));

        for (var tamanho = 1; tamanho <= 4_096; tamanho++)
        {
            var nivel = Buddy.NivelPara(tamanho);

            Assert.True(1 << nivel >= tamanho);
            Assert.True(nivel == 0 || 1 << (nivel - 1) < tamanho);
        }
    }

    /// <summary>
    /// O ACHADO: o pior caso da fragmentacao interna e quase o DOBRO do pedido, e
    /// ele acontece sempre que o pedido passa de uma potencia de dois por um byte.
    /// </summary>
    [Fact]
    public void OPiorCasoGastaQuaseODobro()
    {
        foreach (var potencia in new[] { 5, 6, 7, 10 })
        {
            var pedido = (1 << potencia) + 1;
            var ocupado = 1 << Buddy.NivelPara(pedido);

            Assert.Equal(1 << (potencia + 1), ocupado);
            Assert.True((double)(ocupado - pedido) / ocupado > 0.48);
        }
    }

    /// <summary>
    /// E com pedidos que SAO potencias de dois, o desperdicio e zero.
    /// </summary>
    [Fact]
    public void ComPotenciasDeDoisNaoHaDesperdicio()
    {
        var buddy = new Buddy(16);

        for (var i = 0; i < 100; i++) buddy.Pedir(i, 64);

        Assert.Equal(0, buddy.Desperdicado);
        Assert.Equal(0.0, buddy.FracaoDesperdicada);
    }

    /// <summary>
    /// A DEVOLUCAO junta com o companheiro ate onde der: devolver tudo volta ao
    /// bloco unico do comeco.
    /// </summary>
    [Fact]
    public void DevolverTudoVoltaAoBlocoUnico()
    {
        var buddy = new Buddy(12);

        for (var i = 0; i < 64; i++) Assert.True(buddy.Pedir(i, 64));

        Assert.Equal(4_096, buddy.Ocupado);
        Assert.Equal(0, buddy.Livre());

        for (var i = 0; i < 64; i++) Assert.True(buddy.Devolver(i));

        Assert.Equal(0, buddy.Ocupado);
        Assert.Equal(0, buddy.Pedido);
        Assert.Equal(4_096, buddy.MaiorLivre());
        Assert.True(buddy.ContabilidadeFecha());
    }

    /// <summary>
    /// A CONTA DO PEDIDO e do OCUPADO so conta o que esta vivo AGORA.
    ///
    /// A primeira versao daqui somava todo pedido que ja tinha acontecido e
    /// descontava do ocupado so o que estava vivo. A fracao desperdicada saia
    /// NEGATIVA, que e impossivel, e ela saiu negativa em tres das quatro cargas
    /// sem ninguem estranhar o sinal.
    /// </summary>
    [Fact]
    public void AContaSoContaOQueEstaVivo()
    {
        var buddy = new Buddy(12);

        buddy.Pedir(1, 33);
        Assert.Equal(33, buddy.Pedido);
        Assert.Equal(64, buddy.Ocupado);

        buddy.Pedir(2, 100);
        Assert.Equal(133, buddy.Pedido);
        Assert.Equal(192, buddy.Ocupado);

        buddy.Devolver(1);
        Assert.Equal(100, buddy.Pedido);
        Assert.Equal(128, buddy.Ocupado);

        buddy.Devolver(2);
        Assert.Equal(0, buddy.Pedido);
        Assert.Equal(0, buddy.Ocupado);
    }

    /// <summary>
    /// A FRACAO DESPERDICADA e sempre entre zero e um: ela e uma fracao do que foi
    /// ocupado.
    /// </summary>
    [Fact]
    public void AFracaoDesperdicadaNuncaEhNegativa()
    {
        foreach (var carga in Cargas.Todas(1 << 16))
        {
            var buddy = new Buddy(16);

            foreach (var passo in carga.Passos)
            {
                if (passo.EhPedido) buddy.Pedir(passo.Dono, passo.Tamanho);
                else buddy.Devolver(passo.Dono);

                Assert.InRange(buddy.FracaoDesperdicada, 0.0, 1.0);
                Assert.True(buddy.Desperdicado >= 0);
            }
        }
    }

    [Fact]
    public void PedirMaisQueAMemoriaInteiraEhRecusado()
    {
        var buddy = new Buddy(10);

        Assert.False(buddy.Pedir(1, 2_000));
        Assert.False(buddy.Pedir(1, 0));
        Assert.False(buddy.Pedir(1, -5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Buddy(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Buddy(31));
    }

    [Fact]
    public void PedirDuasVezesComOMesmoDonoEhRecusado()
    {
        var buddy = new Buddy(10);

        Assert.True(buddy.Pedir(1, 100));
        Assert.False(buddy.Pedir(1, 100));
        Assert.False(buddy.Devolver(99));
    }

    /// <summary>
    /// O sistema de companheiros NAO TEM fragmentacao externa no sentido comum:
    /// quando ele recusa, a memoria livre total ja esta abaixo do pedido ou ela
    /// esta repartida em blocos menores por causa do alinhamento.
    ///
    /// E a fragmentacao que ele tem e INTERNA, invisivel para a lista de livres.
    /// </summary>
    [Fact]
    public void AFragmentacaoDoBuddyEhInterna()
    {
        var buddy = new Buddy(16);
        var carga = Cargas.CaudaLonga(2_000, 1 << 16);

        foreach (var passo in carga.Passos)
        {
            if (passo.EhPedido) buddy.Pedir(passo.Dono, passo.Tamanho);
            else buddy.Devolver(passo.Dono);
        }

        Assert.True(buddy.Desperdicado > 0);
        Assert.True(buddy.FracaoDesperdicada > 0.1, $"desperdicio de {buddy.FracaoDesperdicada:P1}");
        Assert.True(buddy.ContabilidadeFecha());
    }
}
