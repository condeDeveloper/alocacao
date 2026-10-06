namespace Conde.Alocacao;

/// <summary>
/// As CARGAS: as sequencias de pedidos e devolucoes que separam as estrategias.
///
/// Uma carga com todos os pedidos do mesmo tamanho nao separa nada: nenhuma
/// estrategia fragmenta, porque toda sobra cabe o proximo pedido exatamente. O
/// que separa e a VARIACAO de tamanho, e ela e enorme em qualquer programa de
/// verdade.
/// </summary>
public static class Cargas
{
    public sealed record Carga(string Nome, List<Juiz.Passo> Passos, int Memoria);

    /// <summary>
    /// Pedidos e devolucoes sorteados, com tamanhos variados: a carga de verdade.
    ///
    /// A cada passo, ou pede um pedaco novo, ou devolve um dos vivos. A proporcao
    /// faz a memoria chegar perto de cheia e ficar la, que e onde a fragmentacao
    /// aparece.
    /// </summary>
    public static Carga Variada(int passos, int memoria, ulong semente = 7, int menor = 1, int maior = 500)
    {
        var sorteio = new Sorteio(semente);
        var lista = new List<Juiz.Passo>();
        var vivos = new List<int>();
        var proximo = 0;

        for (var i = 0; i < passos; i++)
        {
            if (vivos.Count > 0 && sorteio.Fracao() < 0.4)
            {
                var escolhido = sorteio.Ate(vivos.Count);

                lista.Add(new Juiz.Passo(vivos[escolhido], 0, EhPedido: false));
                vivos.RemoveAt(escolhido);

                continue;
            }

            var dono = proximo++;

            lista.Add(new Juiz.Passo(dono, sorteio.Entre(menor, maior), EhPedido: true));
            vivos.Add(dono);
        }

        return new Carga("variada", lista, memoria);
    }

    /// <summary>Todos os pedidos do mesmo tamanho: a carga que nao separa nada.</summary>
    public static Carga Uniforme(int passos, int memoria, int tamanho = 64, ulong semente = 11) =>
        Variada(passos, memoria, semente, tamanho, tamanho) with { Nome = "uniforme" };

    /// <summary>
    /// Tamanhos de CAUDA LONGA: muitos pequenos e poucos enormes.
    ///
    /// E a forma de quase toda carga de verdade, e e onde a escolha do buraco
    /// importa.
    /// </summary>
    public static Carga CaudaLonga(int passos, int memoria, ulong semente = 13)
    {
        var sorteio = new Sorteio(semente);
        var lista = new List<Juiz.Passo>();
        var vivos = new List<int>();
        var proximo = 0;

        for (var i = 0; i < passos; i++)
        {
            if (vivos.Count > 0 && sorteio.Fracao() < 0.4)
            {
                var escolhido = sorteio.Ate(vivos.Count);

                lista.Add(new Juiz.Passo(vivos[escolhido], 0, EhPedido: false));
                vivos.RemoveAt(escolhido);

                continue;
            }

            var tamanho = sorteio.Fracao() < 0.9 ? sorteio.Entre(1, 32) : sorteio.Entre(1_000, 8_000);
            var dono = proximo++;

            lista.Add(new Juiz.Passo(dono, tamanho, EhPedido: true));
            vivos.Add(dono);
        }

        return new Carga("cauda longa", lista, memoria);
    }

    /// <summary>
    /// A carga ALTERNADA: enche a memoria com pares pequeno-grande, devolve TODOS
    /// os pequenos, e pede um bloco do tamanho de tudo que foi devolvido.
    ///
    /// Ela e o retrato da fragmentacao externa, e o ultimo pedido e a frase
    /// inteira: ele cabe na memoria livre somada e nao cabe em nenhum buraco,
    /// porque os buracos sao todos do tamanho do pedaco pequeno.
    ///
    /// A ordem importa: devolver cada pequeno LOGO depois de pedir nao fragmenta
    /// nada, porque o buraco e reusado pelo pequeno seguinte. A primeira versao
    /// daqui fazia assim e a carga nao recusava um pedido sequer.
    /// </summary>
    public static Carga Alternada(int pares, int memoria, int pequeno = 100, int grande = 300)
    {
        var lista = new List<Juiz.Passo>();
        var pequenos = new List<int>();
        var dono = 0;

        for (var i = 0; i < pares; i++)
        {
            var a = dono++;
            var b = dono++;

            lista.Add(new Juiz.Passo(a, pequeno, EhPedido: true));
            lista.Add(new Juiz.Passo(b, grande, EhPedido: true));

            pequenos.Add(a);
        }

        foreach (var a in pequenos) lista.Add(new Juiz.Passo(a, 0, EhPedido: false));

        lista.Add(new Juiz.Passo(dono, pequeno * pares, EhPedido: true));

        return new Carga("alternada", lista, memoria);
    }

    public static IEnumerable<Carga> Todas(int memoria = 100_000)
    {
        yield return Uniforme(4_000, memoria);
        yield return Variada(4_000, memoria);
        yield return CaudaLonga(4_000, memoria);
        yield return Alternada(memoria / 400, memoria);
    }

    /// <summary>Uma sequencia MINUSCULA, para a enumeracao caber.</summary>
    public static Carga Minuscula(int passos, int memoria, ulong semente) =>
        Variada(passos, memoria, semente, memoria / 5, memoria / 2) with { Nome = "minuscula" };
}

/// <summary>
/// As MEDIDAS: as tabelas que o medidor imprime e que os testes conferem.
///
/// Elas ficam aqui, e nao no medidor, para o teste poder afirmar sobre o MESMO
/// numero que aparece no texto. Tabela de README que ninguem testa envelhece
/// errado em silencio.
///
/// Nenhuma delas usa relogio. Todas sao bytes, buracos, recusas ou buracos
/// olhados por pedido.
/// </summary>
public static class Medidas
{
    public sealed record LinhaEstrategia(string Carga, string Estrategia, int Recusados, int Pedidos, int Buracos, int MaiorBuraco, double OlhadosPorPedido);

    /// <summary>
    /// AS QUATRO ESTRATEGIAS em quatro cargas.
    ///
    /// A coluna das recusas e a que importa: ela conta quantos pedidos foram
    /// negados com memoria livre de sobra, que e exatamente o que fragmentacao
    /// externa quer dizer.
    /// </summary>
    public static List<LinhaEstrategia> Estrategias(int memoria = 100_000)
    {
        var linhas = new List<LinhaEstrategia>();

        foreach (var carga in Cargas.Todas(memoria))
            foreach (var estrategia in Estrategia.Todas())
            {
                var alocador = new Alocador(memoria, estrategia);

                foreach (var passo in carga.Passos)
                {
                    if (passo.EhPedido) alocador.Pedir(passo.Dono, passo.Tamanho);
                    else alocador.Devolver(passo.Dono);
                }

                linhas.Add(new LinhaEstrategia(
                    carga.Nome,
                    estrategia.Nome,
                    alocador.Recusados,
                    alocador.Pedidos,
                    alocador.Memoria.BuracosLivres,
                    alocador.Memoria.MaiorBuraco,
                    (double)alocador.BuracosOlhados / alocador.Pedidos));
            }

        return linhas;
    }

    public sealed record LinhaJuiz(int Passos, int Sequencias, double Otimo, double Primeiro, double Melhor, double Pior);

    /// <summary>
    /// O JUIZ: a melhor escolha possivel contra o que cada estrategia fez.
    ///
    /// A enumeracao tenta TODOS os buracos em cada pedido, e o resultado e o menor
    /// numero de recusas que qualquer alocador poderia ter tido.
    /// </summary>
    public static List<LinhaJuiz> Juiz(int sequencias = 200, int memoria = 1_000)
    {
        var linhas = new List<LinhaJuiz>();

        foreach (var passos in new[] { 6, 8, 10 })
        {
            var otimo = 0.0;
            var primeiro = 0.0;
            var melhor = 0.0;
            var pior = 0.0;

            for (ulong semente = 1; semente <= (ulong)sequencias; semente++)
            {
                var carga = Cargas.Minuscula(passos, memoria, semente);

                otimo += Conde.Alocacao.Juiz.MenosRecusasPossivel(memoria, carga.Passos);
                primeiro += Conde.Alocacao.Juiz.RecusasDe(memoria, Estrategia.PrimeiroEncaixe(), carga.Passos);
                melhor += Conde.Alocacao.Juiz.RecusasDe(memoria, Estrategia.MelhorEncaixe(), carga.Passos);
                pior += Conde.Alocacao.Juiz.RecusasDe(memoria, Estrategia.PiorEncaixe(), carga.Passos);
            }

            linhas.Add(new LinhaJuiz(
                passos,
                sequencias,
                otimo / sequencias,
                primeiro / sequencias,
                melhor / sequencias,
                pior / sequencias));
        }

        return linhas;
    }

    public sealed record LinhaJuncao(string Jeito, int Buracos, int MaiorBuraco, int Recusados);

    /// <summary>
    /// A JUNCAO dos buracos vizinhos: a linha que todo mundo esquece na primeira
    /// versao.
    ///
    /// Sem ela nenhum byte some, e a memoria vira po: milhares de buracos pequenos
    /// no lugar de poucos grandes.
    /// </summary>
    public static List<LinhaJuncao> Juncoes(int memoria = 100_000)
    {
        var carga = Cargas.Variada(4_000, memoria);
        var linhas = new List<LinhaJuncao>();

        foreach (var junta in new[] { true, false })
        {
            var alocador = new Alocador(memoria, Estrategia.PrimeiroEncaixe());

            foreach (var passo in carga.Passos)
            {
                if (passo.EhPedido) alocador.Pedir(passo.Dono, passo.Tamanho);
                else if (junta) alocador.Memoria.Devolver(passo.Dono);
                else alocador.Memoria.DevolverSemJuntar(passo.Dono);
            }

            linhas.Add(new LinhaJuncao(
                junta ? "com juncao" : "sem juncao",
                alocador.Memoria.BuracosLivres,
                alocador.Memoria.MaiorBuraco,
                alocador.Recusados));
        }

        return linhas;
    }

    public sealed record LinhaBuddy(string Carga, int Pedido, int Ocupado, double Desperdicio, int MaiorLivre);

    /// <summary>
    /// O SISTEMA DE COMPANHEIROS: sem fragmentacao externa e com a interna que ele
    /// cobra.
    /// </summary>
    public static List<LinhaBuddy> Buddies(int potencia = 20)
    {
        var linhas = new List<LinhaBuddy>();
        var memoria = 1 << potencia;

        foreach (var carga in Cargas.Todas(memoria))
        {
            var buddy = new Buddy(potencia);

            foreach (var passo in carga.Passos)
            {
                if (passo.EhPedido) buddy.Pedir(passo.Dono, passo.Tamanho);
                else buddy.Devolver(passo.Dono);
            }

            linhas.Add(new LinhaBuddy(carga.Nome, buddy.Pedido, buddy.Ocupado, buddy.FracaoDesperdicada, buddy.MaiorLivre()));
        }

        return linhas;
    }

    public sealed record LinhaArredondamento(int Pedido, int Ocupado, double Desperdicio);

    /// <summary>
    /// O ARREDONDAMENTO do sistema de companheiros, pedido a pedido.
    ///
    /// Um pedido de 33 ocupa 64. Um de 65 ocupa 128. O pior caso e pouco menos do
    /// DOBRO, e ele acontece sempre que o pedido passa de uma potencia de dois por
    /// um byte.
    /// </summary>
    public static List<LinhaArredondamento> Arredondamentos()
    {
        var linhas = new List<LinhaArredondamento>();

        foreach (var pedido in new[] { 1, 8, 9, 32, 33, 64, 65, 1_000, 1_025 })
        {
            var ocupado = 1 << Buddy.NivelPara(pedido);

            linhas.Add(new LinhaArredondamento(pedido, ocupado, (double)(ocupado - pedido) / ocupado));
        }

        return linhas;
    }
}
