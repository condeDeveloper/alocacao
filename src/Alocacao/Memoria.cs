namespace Conde.Alocacao;

/// <summary>
/// O SORTEIO, escrito aqui em vez de vir da biblioteca.
///
/// A documentacao do sorteio de biblioteca nao promete a mesma sequencia entre
/// versoes nem entre sistemas, e a integracao continua roda em tres. Uma medida
/// de "quanta memoria o melhor encaixe desperdicou" com pedidos diferentes em
/// cada sistema nao e uma medida: sao tres medidas que ninguem pode comparar.
/// </summary>
public sealed class Sorteio
{
    private ulong estado;

    public Sorteio(ulong semente) => estado = semente == 0 ? 1 : semente;

    public uint Proximo()
    {
        estado = estado * 6364136223846793005UL + 1442695040888963407UL;
        return (uint)(estado >> 32);
    }

    public int Ate(int limite) => (int)(Proximo() % (uint)limite);

    public int Entre(int menor, int maior) => menor + Ate(maior - menor + 1);

    public double Fracao() => Proximo() / 4294967296.0;
}

/// <summary>Um pedaco de memoria: onde comeca, quanto ocupa e de quem e.</summary>
public sealed record Bloco(int Inicio, int Tamanho, int Dono)
{
    public const int Livre = -1;

    public int Fim => Inicio + Tamanho;

    public bool EstaLivre => Dono == Livre;

    public override string ToString() => EstaLivre ? $"[{Inicio}..{Fim}) livre" : $"[{Inicio}..{Fim}) de {Dono}";
}

/// <summary>
/// A MEMORIA: uma fita de bytes dividida em blocos, do comeco ao fim.
///
/// Este e o modelo mais simples que existe e e o modelo de verdade: o alocador de
/// qualquer processo olha uma faixa continua de enderecos e decide onde por cada
/// pedido. Tudo o que muda entre os alocadores daqui e a escolha de QUAL buraco
/// usar.
///
/// A memoria se encarrega de duas coisas que nenhuma estrategia pode errar: cortar
/// o buraco escolhido no tamanho certo, e JUNTAR buracos vizinhos quando alguem
/// devolve. A segunda e a que quase todo mundo esquece na primeira versao, e sem
/// ela a memoria vira po.
/// </summary>
public sealed class Memoria
{
    private readonly List<Bloco> blocos;

    public Memoria(int tamanho)
    {
        if (tamanho <= 0) throw new ArgumentOutOfRangeException(nameof(tamanho));

        Tamanho = tamanho;
        blocos = [new Bloco(0, tamanho, Bloco.Livre)];
    }

    public int Tamanho { get; }

    public IReadOnlyList<Bloco> Blocos => blocos;

    public int Usada => blocos.Where(b => !b.EstaLivre).Sum(b => b.Tamanho);

    public int Livre => Tamanho - Usada;

    public int BuracosLivres => blocos.Count(b => b.EstaLivre);

    public int MaiorBuraco => blocos.Where(b => b.EstaLivre).Select(b => b.Tamanho).DefaultIfEmpty(0).Max();

    /// <summary>Os buracos livres, na ordem em que aparecem na fita.</summary>
    public IEnumerable<(int Indice, Bloco Bloco)> Buracos()
    {
        for (var i = 0; i < blocos.Count; i++)
            if (blocos[i].EstaLivre) yield return (i, blocos[i]);
    }

    /// <summary>
    /// Ocupa um buraco, cortando o que sobrar.
    ///
    /// Devolve o endereco onde o pedido ficou. O resto do buraco vira um buraco
    /// menor, e e esse corte repetido que produz a fragmentacao.
    /// </summary>
    public int Ocupar(int indice, int tamanho, int dono)
    {
        var buraco = blocos[indice];

        if (!buraco.EstaLivre) throw new InvalidOperationException("o bloco escolhido nao esta livre");
        if (buraco.Tamanho < tamanho) throw new InvalidOperationException("o bloco escolhido nao cabe o pedido");

        blocos[indice] = buraco with { Tamanho = tamanho, Dono = dono };

        var sobra = buraco.Tamanho - tamanho;
        if (sobra > 0) blocos.Insert(indice + 1, new Bloco(buraco.Inicio + tamanho, sobra, Bloco.Livre));

        return buraco.Inicio;
    }

    /// <summary>
    /// Devolve o que e de um dono e JUNTA os buracos vizinhos.
    ///
    /// A juncao e a parte que importa: sem ela, devolver dois pedacos vizinhos
    /// deixa dois buracos pequenos no lugar de um grande, e a memoria vira po. Com
    /// ela, a mesma sequencia de pedidos e devolucoes deixa a memoria inteira.
    /// </summary>
    public bool Devolver(int dono)
    {
        var indice = blocos.FindIndex(b => b.Dono == dono);
        if (indice < 0) return false;

        blocos[indice] = blocos[indice] with { Dono = Bloco.Livre };

        Juntar(indice);

        return true;
    }

    private void Juntar(int indice)
    {
        // junta com o de tras primeiro, para o indice continuar valendo
        if (indice + 1 < blocos.Count && blocos[indice + 1].EstaLivre)
        {
            blocos[indice] = blocos[indice] with { Tamanho = blocos[indice].Tamanho + blocos[indice + 1].Tamanho };
            blocos.RemoveAt(indice + 1);
        }

        if (indice > 0 && blocos[indice - 1].EstaLivre)
        {
            blocos[indice - 1] = blocos[indice - 1] with { Tamanho = blocos[indice - 1].Tamanho + blocos[indice].Tamanho };
            blocos.RemoveAt(indice);
        }
    }

    /// <summary>
    /// A MESMA devolucao SEM juntar os vizinhos, para a medida mostrar o estrago.
    /// </summary>
    public bool DevolverSemJuntar(int dono)
    {
        var indice = blocos.FindIndex(b => b.Dono == dono);
        if (indice < 0) return false;

        blocos[indice] = blocos[indice] with { Dono = Bloco.Livre };

        return true;
    }

    /// <summary>
    /// Uma COPIA independente, que a enumeracao do juiz precisa para voltar atras.
    ///
    /// Refazer a memoria a partir dos blocos e muito mais simples e muito menos
    /// sujeito a erro do que reconstruir por alocacoes: a primeira versao daqui
    /// tentava reocupar buraco por buraco e tinha que inventar donos falsos para
    /// os pedacos de antes.
    /// </summary>
    public Memoria Clonar()
    {
        var copia = new Memoria(Tamanho);

        copia.blocos.Clear();
        copia.blocos.AddRange(blocos);

        return copia;
    }

    public string Desenho(int largura = 80)
    {
        var letras = new char[largura];

        for (var i = 0; i < largura; i++)
        {
            var posicao = (long)i * Tamanho / largura;
            var bloco = blocos.First(b => b.Inicio <= posicao && posicao < b.Fim);

            letras[i] = bloco.EstaLivre ? '.' : (char)('a' + bloco.Dono % 26);
        }

        return new string(letras);
    }
}
