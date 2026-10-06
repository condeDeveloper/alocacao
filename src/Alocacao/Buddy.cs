namespace Conde.Alocacao;

/// <summary>
/// O SISTEMA DE COMPANHEIROS, de Knuth: so tamanhos potencia de dois.
///
/// Toda a memoria e um bloco de 2^n. Um pedido menor parte o bloco ao meio,
/// repetidamente, ate chegar na potencia de dois que cabe o pedido. As duas
/// metades de um corte sao COMPANHEIRAS, e quando as duas estao livres elas se
/// juntam de novo, automaticamente.
///
/// O que ele compra: nada de fragmentacao externa de verdade, e a juncao e de
/// graca. Achar o companheiro de um bloco e um ou-exclusivo com o tamanho, e so.
///
/// O que ele custa: FRAGMENTACAO INTERNA. Um pedido de 33 bytes ocupa 64, e os 31
/// bytes que sobram ficam perdidos dentro do bloco, invisiveis para o alocador. No
/// pior caso ele gasta quase o DOBRO do pedido, e a medida mostra o quanto isso
/// acontece de verdade.
/// </summary>
public sealed class Buddy
{
    private readonly List<int>[] livres;
    private readonly Dictionary<int, (int Endereco, int Nivel)> ocupados = [];
    private readonly Dictionary<int, int> pedidoPorDono = [];

    public Buddy(int potencia)
    {
        if (potencia < 1 || potencia > 30) throw new ArgumentOutOfRangeException(nameof(potencia));

        Potencia = potencia;
        Tamanho = 1 << potencia;

        livres = new List<int>[potencia + 1];
        for (var i = 0; i <= potencia; i++) livres[i] = [];

        livres[potencia].Add(0);
    }

    /// <summary>O expoente do tamanho total.</summary>
    public int Potencia { get; }

    public int Tamanho { get; }

    /// <summary>
    /// A soma do que esta PEDIDO AGORA, sem o arredondamento.
    ///
    /// "Agora" e a palavra que importa: a primeira versao daqui somava todo pedido
    /// que ja tinha acontecido e descontava do ocupado so o que estava vivo. A
    /// fracao desperdicada saia NEGATIVA, que e impossivel, e ela saiu negativa em
    /// tres das quatro cargas sem ninguem estranhar o sinal.
    /// </summary>
    public int Pedido { get; private set; }

    /// <summary>A soma do que esta OCUPADO agora, com o arredondamento.</summary>
    public int Ocupado { get; private set; }

    /// <summary>
    /// A FRAGMENTACAO INTERNA: o que o arredondamento desperdicou.
    ///
    /// Ela e invisivel para o alocador: o bloco esta ocupado, a lista de livres nao
    /// sabe que sobrou espaco dentro dele, e nenhum pedido novo vai caber ali.
    /// </summary>
    public int Desperdicado => Ocupado - Pedido;

    public double FracaoDesperdicada => Ocupado == 0 ? 0 : (double)Desperdicado / Ocupado;

    public int Blocos(int nivel) => livres[nivel].Count;

    public int MaiorLivre()
    {
        for (var nivel = Potencia; nivel >= 0; nivel--)
            if (livres[nivel].Count > 0) return 1 << nivel;

        return 0;
    }

    /// <summary>
    /// O menor expoente cujo bloco cabe o pedido.
    ///
    /// E aqui que a fragmentacao interna nasce: um pedido de 33 vira um bloco de
    /// 64, e nada depois disso desfaz esse arredondamento.
    /// </summary>
    public static int NivelPara(int tamanho)
    {
        var nivel = 0;
        while (1 << nivel < tamanho) nivel++;

        return nivel;
    }

    public bool Pedir(int dono, int tamanho)
    {
        if (tamanho <= 0 || tamanho > Tamanho) return false;
        if (ocupados.ContainsKey(dono)) return false;

        var alvo = NivelPara(tamanho);

        // acha o menor bloco livre que serve
        var nivel = alvo;
        while (nivel <= Potencia && livres[nivel].Count == 0) nivel++;

        if (nivel > Potencia) return false;

        var endereco = livres[nivel][0];
        livres[nivel].RemoveAt(0);

        // parte ao meio ate chegar no alvo, guardando cada companheiro
        while (nivel > alvo)
        {
            nivel--;
            livres[nivel].Add(endereco + (1 << nivel));
            livres[nivel].Sort();
        }

        ocupados[dono] = (endereco, alvo);
        pedidoPorDono[dono] = tamanho;

        Pedido += tamanho;
        Ocupado += 1 << alvo;

        return true;
    }

    /// <summary>
    /// Devolve e junta com o COMPANHEIRO enquanto ele estiver livre.
    ///
    /// O companheiro de um bloco e o endereco com o bit do tamanho invertido, e e
    /// so isso: um ou-exclusivo. Essa conta e a razao inteira de o sistema existir,
    /// porque ela torna a juncao imediata, sem procurar nada.
    /// </summary>
    public bool Devolver(int dono)
    {
        if (!ocupados.TryGetValue(dono, out var onde)) return false;

        ocupados.Remove(dono);
        Ocupado -= 1 << onde.Nivel;
        Pedido -= pedidoPorDono[dono];
        pedidoPorDono.Remove(dono);

        var (endereco, nivel) = onde;

        while (nivel < Potencia)
        {
            var companheiro = endereco ^ (1 << nivel);

            if (!livres[nivel].Remove(companheiro)) break;

            endereco = Math.Min(endereco, companheiro);
            nivel++;
        }

        livres[nivel].Add(endereco);
        livres[nivel].Sort();

        return true;
    }

    /// <summary>O companheiro de um bloco, que e um ou-exclusivo e nada mais.</summary>
    public static int Companheiro(int endereco, int nivel) => endereco ^ (1 << nivel);

    /// <summary>
    /// A memoria livre de verdade, somando todos os blocos de todos os niveis.
    /// </summary>
    public int Livre()
    {
        var total = 0;
        for (var nivel = 0; nivel <= Potencia; nivel++) total += livres[nivel].Count * (1 << nivel);

        return total;
    }

    /// <summary>
    /// A conferencia que torna tudo o mais confiavel: a soma do livre com o ocupado
    /// e a memoria inteira, e nenhum bloco livre se sobrepoe a outro.
    /// </summary>
    public bool ContabilidadeFecha()
    {
        if (Livre() + Ocupado != Tamanho) return false;

        var usados = new bool[Tamanho];

        foreach (var (_, onde) in ocupados)
            for (var i = 0; i < 1 << onde.Nivel; i++)
            {
                if (usados[onde.Endereco + i]) return false;
                usados[onde.Endereco + i] = true;
            }

        for (var nivel = 0; nivel <= Potencia; nivel++)
            foreach (var endereco in livres[nivel])
                for (var i = 0; i < 1 << nivel; i++)
                {
                    if (usados[endereco + i]) return false;
                    usados[endereco + i] = true;
                }

        return usados.All(u => u);
    }

    /// <summary>
    /// Todo bloco livre esta ALINHADO ao tamanho dele, que e a propriedade que faz
    /// a conta do companheiro funcionar.
    /// </summary>
    public bool TodoBlocoEstaAlinhado()
    {
        for (var nivel = 0; nivel <= Potencia; nivel++)
            foreach (var endereco in livres[nivel])
                if (endereco % (1 << nivel) != 0) return false;

        foreach (var (_, onde) in ocupados)
            if (onde.Endereco % (1 << onde.Nivel) != 0) return false;

        return true;
    }
}
