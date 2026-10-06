using Conde.Alocacao;
using Xunit;

namespace Conde.Alocacao.Testes;

/// <summary>
/// A CONTABILIDADE da memoria, que e a primeira metade do juiz.
///
/// A memoria e uma fita de bytes, e depois de qualquer sequencia de pedidos e
/// devolucoes ela precisa continuar sendo exatamente isso: nenhum byte a mais,
/// nenhum a menos, nenhum em dois lugares. Se essas conferencias nao valessem,
/// todas as tabelas estariam medindo outra coisa.
/// </summary>
public class MemoriaTestes
{
    [Fact]
    public void OSorteioEhDeterminista()
    {
        var a = new Sorteio(7);
        var b = new Sorteio(7);

        for (var i = 0; i < 1000; i++) Assert.Equal(a.Proximo(), b.Proximo());
    }

    /// <summary>
    /// A fracao precisa cobrir os DOIS LADOS do meio: um deslocamento de 33 em vez
    /// de 32 deixaria o gerador com 31 bits e "devolve com probabilidade 0,4"
    /// viraria "devolve sempre", com os numeros continuando plausiveis.
    /// </summary>
    [Fact]
    public void AFracaoCobreOsDoisLadosDoMeio()
    {
        var sorteio = new Sorteio(7);
        var acima = 0;
        var maior = 0.0;

        for (var i = 0; i < 10_000; i++)
        {
            var f = sorteio.Fracao();

            Assert.InRange(f, 0.0, 1.0);
            if (f > 0.5) acima++;
            if (f > maior) maior = f;
        }

        Assert.InRange(acima, 4_700, 5_300);
        Assert.True(maior > 0.999, $"o maior sorteio foi {maior}");
    }

    [Fact]
    public void AMemoriaNovaEhUmBuracoSo()
    {
        var memoria = new Memoria(1_000);

        Assert.Single(memoria.Blocos);
        Assert.Equal(1_000, memoria.Livre);
        Assert.Equal(0, memoria.Usada);
        Assert.Equal(1_000, memoria.MaiorBuraco);
        Assert.True(Juiz.AFitaEstaInteira(memoria));
    }

    [Fact]
    public void OcuparCortaOBuracoNoTamanhoCerto()
    {
        var memoria = new Memoria(100);
        var endereco = memoria.Ocupar(0, 30, dono: 1);

        Assert.Equal(0, endereco);
        Assert.Equal(2, memoria.Blocos.Count);
        Assert.Equal(30, memoria.Usada);
        Assert.Equal(70, memoria.Livre);
        Assert.True(Juiz.AFitaEstaInteira(memoria));
    }

    [Fact]
    public void OcuparOBuracoInteiroNaoDeixaSobra()
    {
        var memoria = new Memoria(100);
        memoria.Ocupar(0, 100, dono: 1);

        Assert.Single(memoria.Blocos);
        Assert.Equal(0, memoria.BuracosLivres);
        Assert.True(Juiz.AFitaEstaInteira(memoria));
    }

    [Fact]
    public void OcuparUmBlocoQueNaoCabeOuQueNaoEstaLivreEhRecusado()
    {
        var memoria = new Memoria(100);
        memoria.Ocupar(0, 30, dono: 1);

        Assert.Throws<InvalidOperationException>(() => memoria.Ocupar(0, 10, dono: 2));
        Assert.Throws<InvalidOperationException>(() => memoria.Ocupar(1, 200, dono: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Memoria(0));
    }

    /// <summary>
    /// A JUNCAO dos buracos vizinhos: devolver dois pedacos grudados deixa UM
    /// buraco, nao dois.
    ///
    /// E a parte que quase todo mundo esquece na primeira versao, e sem ela nenhum
    /// byte some e a memoria vira po.
    /// </summary>
    [Fact]
    public void DevolverJuntaOsBuracosVizinhos()
    {
        var memoria = new Memoria(100);

        memoria.Ocupar(0, 30, dono: 1);
        memoria.Ocupar(1, 30, dono: 2);

        Assert.Equal(3, memoria.Blocos.Count);

        memoria.Devolver(1);
        memoria.Devolver(2);

        Assert.Single(memoria.Blocos);
        Assert.Equal(100, memoria.MaiorBuraco);
        Assert.True(Juiz.NaoHaBuracosGrudados(memoria));
    }

    /// <summary>
    /// E SEM a juncao os mesmos dois pedacos deixam dois buracos: o mesmo numero de
    /// bytes livres, repartido de um jeito que nao serve.
    /// </summary>
    [Fact]
    public void SemJuntarOsMesmosBytesViramDoisBuracos()
    {
        var memoria = new Memoria(100);

        memoria.Ocupar(0, 30, dono: 1);
        memoria.Ocupar(1, 30, dono: 2);

        memoria.DevolverSemJuntar(1);
        memoria.DevolverSemJuntar(2);

        Assert.Equal(100, memoria.Livre);
        Assert.Equal(3, memoria.BuracosLivres);
        Assert.Equal(40, memoria.MaiorBuraco);
        Assert.False(Juiz.NaoHaBuracosGrudados(memoria));
    }

    [Fact]
    public void AJuncaoFuncionaDosDoisLados()
    {
        var memoria = new Memoria(90);

        memoria.Ocupar(0, 30, dono: 1);
        memoria.Ocupar(1, 30, dono: 2);
        memoria.Ocupar(2, 30, dono: 3);

        memoria.Devolver(1);
        memoria.Devolver(3);

        Assert.Equal(3, memoria.Blocos.Count);

        // o do meio junta com os dois vizinhos de uma vez
        memoria.Devolver(2);

        Assert.Single(memoria.Blocos);
        Assert.Equal(90, memoria.MaiorBuraco);
    }

    [Fact]
    public void DevolverQuemNaoExisteNaoFazNada()
    {
        var memoria = new Memoria(100);

        Assert.False(memoria.Devolver(99));
        Assert.False(memoria.DevolverSemJuntar(99));
    }

    /// <summary>
    /// A FITA continua inteira depois de QUALQUER sequencia, em toda estrategia.
    ///
    /// E a conferencia e feita DEPOIS DE CADA PASSO: conferir so no fim deixa
    /// passar um estado quebrado no meio que o passo seguinte conserta por acaso.
    /// </summary>
    [Fact]
    public void AFitaContinuaInteiraDepoisDeCadaPasso()
    {
        foreach (var carga in Cargas.Todas(10_000))
            foreach (var estrategia in Estrategia.Todas())
            {
                var alocador = new Alocador(10_000, estrategia);

                foreach (var passo in carga.Passos)
                {
                    if (passo.EhPedido) alocador.Pedir(passo.Dono, passo.Tamanho);
                    else alocador.Devolver(passo.Dono);

                    Assert.True(Juiz.TudoCerto(alocador), $"{carga.Nome} com {estrategia.Nome}");
                }
            }
    }

    /// <summary>
    /// Devolvendo TUDO, a memoria volta a ser um buraco so: nada se perde pelo
    /// caminho.
    /// </summary>
    [Fact]
    public void DevolvendoTudoAMemoriaVoltaAoComeco()
    {
        foreach (var estrategia in Estrategia.Todas())
        {
            var alocador = new Alocador(10_000, estrategia);
            var carga = Cargas.Variada(1_000, 10_000);

            foreach (var passo in carga.Passos)
            {
                if (passo.EhPedido) alocador.Pedir(passo.Dono, passo.Tamanho);
                else alocador.Devolver(passo.Dono);
            }

            foreach (var dono in alocador.Vivos.Keys.ToList()) alocador.Devolver(dono);

            Assert.Single(alocador.Memoria.Blocos);
            Assert.Equal(10_000, alocador.Memoria.MaiorBuraco);
        }
    }

    [Fact]
    public void ClonarNaoCompartilhaOsBlocos()
    {
        var memoria = new Memoria(100);
        memoria.Ocupar(0, 30, dono: 1);

        var copia = memoria.Clonar();
        copia.Ocupar(1, 20, dono: 2);

        Assert.Equal(30, memoria.Usada);
        Assert.Equal(50, copia.Usada);
        Assert.True(Juiz.AFitaEstaInteira(memoria));
        Assert.True(Juiz.AFitaEstaInteira(copia));
    }

    [Fact]
    public void ODesenhoMostraOQueEstaOndeEstá()
    {
        var memoria = new Memoria(100);
        memoria.Ocupar(0, 50, dono: 0);

        var desenho = memoria.Desenho(10);

        Assert.Equal("aaaaa.....", desenho);
    }
}
