namespace Conde.Alocacao;

/// <summary>
/// Uma ESTRATEGIA de alocacao: entre os buracos que cabem, qual usar.
///
/// Tudo o que distingue os alocadores classicos cabe nessa pergunta. Nao ha
/// estrutura diferente, nao ha algoritmo diferente: ha uma escolha por pedido, e
/// as diferencas de desperdicio sao de dezenas de por cento.
/// </summary>
public abstract record Estrategia(string Nome)
{
    /// <summary>
    /// Qual buraco usar, ou menos um se nenhum cabe.
    ///
    /// Recebe a memoria inteira porque algumas estrategias olham o estado de fora
    /// do pedido: a do proximo encaixe lembra onde parou da ultima vez.
    /// </summary>
    public abstract int Escolher(Memoria memoria, int tamanho);

    /// <summary>Quantos buracos a estrategia olhou no ultimo pedido.</summary>
    public int Olhados { get; protected set; }

    public virtual void Recomecar() => Olhados = 0;

    public sealed override string ToString() => Nome;

    /// <summary>
    /// PRIMEIRO ENCAIXE: o primeiro buraco que cabe.
    ///
    /// Ele e o mais rapido de todos e ele e, na pratica, tao bom quanto o melhor
    /// encaixe. A medida daqui mostra isso, e e um dos resultados mais antigos e
    /// menos conhecidos da area.
    /// </summary>
    public static Estrategia PrimeiroEncaixe() => new PorPrimeiro();

    /// <summary>
    /// MELHOR ENCAIXE: o menor buraco que cabe.
    ///
    /// O nome promete o que ele nao entrega. Escolher o buraco mais justo deixa a
    /// SOBRA mais pequena possivel, e uma sobra pequena demais nao serve para mais
    /// nada: ela vira uma lasca que fica ocupando lugar na lista para sempre.
    /// </summary>
    public static Estrategia MelhorEncaixe() => new PorMelhor();

    /// <summary>
    /// PIOR ENCAIXE: o maior buraco que cabe.
    ///
    /// A ideia e a oposta e e defensavel: deixar a sobra GRANDE o bastante para ser
    /// usada de novo. Na pratica ele destroi os buracos grandes e e o pior dos
    /// tres, e a medida mostra por quanto.
    /// </summary>
    public static Estrategia PiorEncaixe() => new PorPior();

    /// <summary>
    /// PROXIMO ENCAIXE: o primeiro que cabe, comecando de onde parou.
    ///
    /// Ele existe para nao varrer a lista desde o inicio em todo pedido, e ele
    /// espalha os pedidos pela memoria inteira em vez de amontoar no comeco.
    /// </summary>
    public static Estrategia ProximoEncaixe() => new PorProximo();

    public static IEnumerable<Estrategia> Todas()
    {
        yield return PrimeiroEncaixe();
        yield return MelhorEncaixe();
        yield return PiorEncaixe();
        yield return ProximoEncaixe();
    }

    private sealed record PorPrimeiro() : Estrategia("primeiro encaixe")
    {
        public override int Escolher(Memoria memoria, int tamanho)
        {
            ArgumentNullException.ThrowIfNull(memoria);
            Olhados = 0;

            foreach (var (indice, bloco) in memoria.Buracos())
            {
                Olhados++;
                if (bloco.Tamanho >= tamanho) return indice;
            }

            return -1;
        }
    }

    private sealed record PorMelhor() : Estrategia("melhor encaixe")
    {
        public override int Escolher(Memoria memoria, int tamanho)
        {
            ArgumentNullException.ThrowIfNull(memoria);
            Olhados = 0;

            var escolhido = -1;
            var menor = int.MaxValue;

            foreach (var (indice, bloco) in memoria.Buracos())
            {
                Olhados++;
                if (bloco.Tamanho < tamanho || bloco.Tamanho >= menor) continue;

                menor = bloco.Tamanho;
                escolhido = indice;
            }

            return escolhido;
        }
    }

    private sealed record PorPior() : Estrategia("pior encaixe")
    {
        public override int Escolher(Memoria memoria, int tamanho)
        {
            ArgumentNullException.ThrowIfNull(memoria);
            Olhados = 0;

            var escolhido = -1;
            var maior = -1;

            foreach (var (indice, bloco) in memoria.Buracos())
            {
                Olhados++;
                if (bloco.Tamanho < tamanho || bloco.Tamanho <= maior) continue;

                maior = bloco.Tamanho;
                escolhido = indice;
            }

            return escolhido;
        }
    }

    private sealed record PorProximo : Estrategia
    {
        private int ondeParou;

        public PorProximo() : base("proximo encaixe") { }

        public override void Recomecar()
        {
            base.Recomecar();
            ondeParou = 0;
        }

        public override int Escolher(Memoria memoria, int tamanho)
        {
            ArgumentNullException.ThrowIfNull(memoria);
            Olhados = 0;

            var buracos = memoria.Buracos().ToList();
            if (buracos.Count == 0) return -1;

            // comeca de onde parou e da a volta: e isso que espalha os pedidos
            // pela memoria inteira em vez de amontoar no comeco
            for (var i = 0; i < buracos.Count; i++)
            {
                var pos = (ondeParou + i) % buracos.Count;
                Olhados++;

                if (buracos[pos].Bloco.Tamanho < tamanho) continue;

                ondeParou = pos;

                return buracos[pos].Indice;
            }

            return -1;
        }
    }
}

/// <summary>
/// O ALOCADOR: a memoria mais uma estrategia, com a contabilidade de tudo que
/// aconteceu.
/// </summary>
public sealed class Alocador
{
    private readonly Dictionary<int, int> tamanhoPorDono = [];

    public Alocador(int tamanho, Estrategia estrategia)
    {
        ArgumentNullException.ThrowIfNull(estrategia);

        Memoria = new Memoria(tamanho);
        Estrategia = estrategia;
        estrategia.Recomecar();
    }

    public Memoria Memoria { get; }

    public Estrategia Estrategia { get; }

    public int Pedidos { get; private set; }

    public int Recusados { get; private set; }

    public long BuracosOlhados { get; private set; }

    /// <summary>Tenta alocar e devolve se conseguiu.</summary>
    public bool Pedir(int dono, int tamanho)
    {
        Pedidos++;

        var indice = Estrategia.Escolher(Memoria, tamanho);
        BuracosOlhados += Estrategia.Olhados;

        if (indice < 0)
        {
            Recusados++;
            return false;
        }

        Memoria.Ocupar(indice, tamanho, dono);
        tamanhoPorDono[dono] = tamanho;

        return true;
    }

    public bool Devolver(int dono)
    {
        tamanhoPorDono.Remove(dono);

        return Memoria.Devolver(dono);
    }

    public IReadOnlyDictionary<int, int> Vivos => tamanhoPorDono;
}
