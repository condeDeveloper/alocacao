namespace Conde.Alocacao;

/// <summary>
/// O JUIZ deste repositorio, em duas partes.
///
/// A primeira e a CONTABILIDADE: a memoria e uma fita de bytes, e depois de
/// qualquer sequencia de pedidos e devolucoes ela precisa continuar sendo
/// exatamente isso. Nenhum byte a mais, nenhum a menos, nenhum em dois lugares.
/// Essas conferencias sao baratas e rodam depois de cada operacao.
///
/// A segunda e a ENUMERACAO: para sequencias minusculas, todas as escolhas
/// possiveis de buraco sao exploradas, e a melhor delas e a resposta otima. So
/// assim da para dizer que uma estrategia desperdiçou, em vez de dizer que ela
/// desperdicou MAIS QUE A OUTRA.
/// </summary>
public static class Juiz
{
    /// <summary>
    /// Os blocos cobrem a fita inteira, em ordem, sem buraco e sem sobreposicao.
    /// </summary>
    public static bool AFitaEstaInteira(Memoria memoria)
    {
        ArgumentNullException.ThrowIfNull(memoria);

        var esperado = 0;

        foreach (var bloco in memoria.Blocos)
        {
            if (bloco.Inicio != esperado) return false;
            if (bloco.Tamanho <= 0) return false;

            esperado = bloco.Fim;
        }

        return esperado == memoria.Tamanho;
    }

    /// <summary>
    /// Nao ha dois buracos livres GRUDADOS, que e o sinal de que a juncao
    /// aconteceu.
    ///
    /// E a conferencia que pega o esquecimento mais comum do assunto: devolver sem
    /// juntar deixa dois buracos pequenos onde deveria haver um grande, e a memoria
    /// vira po sem nenhum byte ter sumido.
    /// </summary>
    public static bool NaoHaBuracosGrudados(Memoria memoria)
    {
        ArgumentNullException.ThrowIfNull(memoria);

        for (var i = 1; i < memoria.Blocos.Count; i++)
            if (memoria.Blocos[i].EstaLivre && memoria.Blocos[i - 1].EstaLivre) return false;

        return true;
    }

    /// <summary>Cada dono aparece no maximo uma vez.</summary>
    public static bool CadaDonoApareceUmaVez(Memoria memoria)
    {
        ArgumentNullException.ThrowIfNull(memoria);

        var donos = memoria.Blocos.Where(b => !b.EstaLivre).Select(b => b.Dono).ToList();

        return donos.Count == donos.Distinct().Count();
    }

    /// <summary>
    /// O que o alocador ACHA que entregou bate com o que esta na fita.
    /// </summary>
    public static bool AContabilidadeBate(Alocador alocador)
    {
        ArgumentNullException.ThrowIfNull(alocador);

        foreach (var (dono, tamanho) in alocador.Vivos)
        {
            var bloco = alocador.Memoria.Blocos.SingleOrDefault(b => b.Dono == dono);

            if (bloco is null || bloco.Tamanho != tamanho) return false;
        }

        var naFita = alocador.Memoria.Blocos.Count(b => !b.EstaLivre);

        return naFita == alocador.Vivos.Count;
    }

    public static bool TudoCerto(Alocador alocador)
    {
        ArgumentNullException.ThrowIfNull(alocador);

        return AFitaEstaInteira(alocador.Memoria) &&
               NaoHaBuracosGrudados(alocador.Memoria) &&
               CadaDonoApareceUmaVez(alocador.Memoria) &&
               AContabilidadeBate(alocador);
    }

    /// <summary>Um pedido de uma sequencia: alocar um tamanho ou devolver um dono.</summary>
    public sealed record Passo(int Dono, int Tamanho, bool EhPedido)
    {
        public override string ToString() => EhPedido ? $"pedir {Tamanho} para {Dono}" : $"devolver {Dono}";
    }

    /// <summary>
    /// A MELHOR escolha possivel, por enumeracao de todas as decisoes.
    ///
    /// Em cada pedido, tenta TODOS os buracos que cabem, e segue. O resultado e o
    /// menor numero de recusas que qualquer alocador poderia ter tido com aquela
    /// sequencia, e com ele da para dizer o quanto cada estrategia deixou na mesa.
    ///
    /// E exponencial e so cabe em sequencias minusculas, e e exatamente o tamanho
    /// em que uma estrategia ruim ainda parece razoavel.
    /// </summary>
    public static int MenosRecusasPossivel(int tamanhoDaMemoria, IReadOnlyList<Passo> passos, int limite = 12)
    {
        ArgumentNullException.ThrowIfNull(passos);
        if (passos.Count > limite) throw new ArgumentException($"a enumeracao so cabe em ate {limite} passos", nameof(passos));

        var melhor = passos.Count;

        void Andar(Memoria memoria, int passo, int recusas)
        {
            if (recusas >= melhor) return;

            if (passo == passos.Count)
            {
                melhor = recusas;
                return;
            }

            var atual = passos[passo];

            if (!atual.EhPedido)
            {
                var copia = memoria.Clonar();
                copia.Devolver(atual.Dono);

                Andar(copia, passo + 1, recusas);

                return;
            }

            var cabem = memoria.Buracos().Where(b => b.Bloco.Tamanho >= atual.Tamanho).ToList();

            if (cabem.Count == 0)
            {
                Andar(memoria, passo + 1, recusas + 1);
                return;
            }

            foreach (var (indice, _) in cabem)
            {
                var copia = memoria.Clonar();
                copia.Ocupar(indice, atual.Tamanho, atual.Dono);

                Andar(copia, passo + 1, recusas);
            }
        }

        Andar(new Memoria(tamanhoDaMemoria), 0, 0);

        return melhor;
    }

    /// <summary>
    /// Quantas recusas uma estrategia teve naquela sequencia.
    /// </summary>
    public static int RecusasDe(int tamanhoDaMemoria, Estrategia estrategia, IReadOnlyList<Passo> passos)
    {
        ArgumentNullException.ThrowIfNull(passos);

        var alocador = new Alocador(tamanhoDaMemoria, estrategia);

        foreach (var passo in passos)
        {
            if (passo.EhPedido) alocador.Pedir(passo.Dono, passo.Tamanho);
            else alocador.Devolver(passo.Dono);
        }

        return alocador.Recusados;
    }
}
