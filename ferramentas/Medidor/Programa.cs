using Conde.Alocacao;

var pedido = args.Length > 0 ? args[0] : "tudo";
var tudo = pedido == "tudo";

if (tudo || pedido == "juiz") Juizes();
if (tudo || pedido == "estrategias") Estrategias();
if (tudo || pedido == "juncao") Juncoes();
if (tudo || pedido == "buddy") Buddies();

static void Juizes()
{
    Console.WriteLine();
    Console.WriteLine("  O JUIZ: A MELHOR ESCOLHA POSSIVEL CONTRA O QUE CADA ESTRATEGIA FEZ");
    Console.WriteLine("  (recusas medias em 200 sequencias sorteadas de cada tamanho, memoria de 1.000)");
    Console.WriteLine();
    Console.WriteLine("  passos   sequencias     otimo   primeiro encaixe   melhor encaixe   pior encaixe");

    foreach (var l in Medidas.Juiz())
        Console.WriteLine($"  {l.Passos,6} {l.Sequencias,12} {l.Otimo,9:F2} {l.Primeiro,18:F2} {l.Melhor,16:F2} {l.Pior,14:F2}");

    Console.WriteLine();
}

static void Estrategias()
{
    Console.WriteLine();
    Console.WriteLine("  AS QUATRO ESTRATEGIAS EM QUATRO CARGAS (memoria de 100.000)");
    Console.WriteLine();
    Console.WriteLine("  carga          estrategia           recusados   pedidos   buracos   maior buraco   olhados por pedido");

    var anterior = "";
    foreach (var l in Medidas.Estrategias())
    {
        if (l.Carga != anterior) { Console.WriteLine(); anterior = l.Carga; }

        Console.WriteLine($"  {l.Carga,-14} {l.Estrategia,-20} {l.Recusados,9:N0} {l.Pedidos,9:N0} {l.Buracos,9:N0} {l.MaiorBuraco,14:N0} {l.OlhadosPorPedido,20:F1}");
    }

    Console.WriteLine();
}

static void Juncoes()
{
    Console.WriteLine();
    Console.WriteLine("  A JUNCAO DOS BURACOS VIZINHOS: A LINHA QUE TODO MUNDO ESQUECE");
    Console.WriteLine();
    Console.WriteLine("  jeito          buracos no fim   maior buraco   pedidos recusados");

    foreach (var l in Medidas.Juncoes())
        Console.WriteLine($"  {l.Jeito,-14} {l.Buracos,16:N0} {l.MaiorBuraco,14:N0} {l.Recusados,19:N0}");

    Console.WriteLine();
    Console.WriteLine("  nenhum byte some sem a juncao: a memoria vira po");
    Console.WriteLine();
}

static void Buddies()
{
    Console.WriteLine();
    Console.WriteLine("  O SISTEMA DE COMPANHEIROS (memoria de 2^20)");
    Console.WriteLine();
    Console.WriteLine("  carga             pedido       ocupado   desperdicio   maior livre");

    foreach (var l in Medidas.Buddies())
        Console.WriteLine($"  {l.Carga,-14} {l.Pedido,10:N0} {l.Ocupado,13:N0} {l.Desperdicio,13:P1} {l.MaiorLivre,13:N0}");

    Console.WriteLine();
    Console.WriteLine("  O ARREDONDAMENTO, PEDIDO A PEDIDO");
    Console.WriteLine();
    Console.WriteLine("  pedido   ocupado   desperdicio");

    foreach (var l in Medidas.Arredondamentos())
        Console.WriteLine($"  {l.Pedido,6:N0} {l.Ocupado,9:N0} {l.Desperdicio,13:P1}");

    Console.WriteLine();
}
