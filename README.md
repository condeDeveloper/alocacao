# alocacao

Alocação de memória do zero em C# e .NET 8: primeiro encaixe, melhor encaixe,
pior encaixe, próximo encaixe e o sistema de companheiros. O juiz é a
**contabilidade byte a byte**, conferida depois de cada operação, e a **enumeração
de todas as escolhas de buraco**.

```
$ dotnet medidor.dll estrategias

  carga          estrategia           recusados   pedidos   buracos   maior buraco   olhados por pedido

  uniforme       primeiro encaixe             0     2,417         2         46,560                  1.0
  uniforme       melhor encaixe               0     2,417         2         46,560                  2.9
  uniforme       pior encaixe                 0     2,417       319            320                157.7

  variada        primeiro encaixe           358     2,345       214            234                 78.6
  variada        melhor encaixe             340     2,345       172            211                102.3
  variada        pior encaixe               442     2,345       203            253                117.5

  cauda longa    primeiro encaixe            89     2,360       199          5,363                 53.6
  cauda longa    melhor encaixe              95     2,360       130          5,017                 65.6
  cauda longa    pior encaixe               122     2,360       287          5,270                145.2
```

O primeiro bloco é por que o problema passa despercebido: com pedidos todos do
mesmo tamanho, **ninguém fragmenta**. Toda sobra cabe o próximo pedido
exatamente, e três das quatro estratégias dão a mesma memória no fim. Medir
alocação com pedidos de tamanho fixo não descobre nada.

A coluna das recusas é a que importa: ela conta quantos pedidos foram negados
**com memória livre de sobra**, que é exatamente o que fragmentação externa quer
dizer.

## O melhor encaixe não é o melhor

O nome promete o que ele não entrega. Escolher o buraco mais justo deixa a sobra
mais pequena possível, e uma sobra pequena demais não serve para mais nada: ela
vira uma lasca que fica ocupando lugar na lista para sempre.

Na carga de cauda longa ele recusa 95 pedidos contra 89 do primeiro encaixe, que
é o mais burro dos quatro, e olha vinte e cinco por cento mais buracos para
decidir.

E o **pior** encaixe é mesmo o pior, nas duas cargas com variação de tamanho. A
ideia dele é defensável (deixar a sobra grande o bastante para ser usada de novo)
e ela destrói os buracos grandes: na carga uniforme ele acaba com **319 buracos**
de 320 bytes cada, onde os outros três terminam com dois buracos e um deles com
46 mil bytes.

## Ter espaço e não ter espaço contínuo

```
  alternada      primeiro encaixe             1       501       250            100                  1.5
```

A carga alternada é o retrato da fragmentação externa: ela enche a memória com
pares pequeno-grande, devolve **todos** os pequenos, e pede um bloco do tamanho de
tudo que foi devolvido.

O último pedido cabe na memória livre somada e não cabe em nenhum buraco, porque
os buracos são todos do tamanho do pedaço pequeno. As quatro estratégias recusam,
e nenhuma escolha de buraco teria salvado: o problema não é a escolha, é a forma.

## A linha que todo mundo esquece

```
$ dotnet medidor.dll juncao

  jeito          buracos no fim   maior buraco   pedidos recusados
  com juncao                214            234                 358
  sem juncao               1,290            201                 571
```

Devolver sem juntar os buracos vizinhos não perde um byte. A memória livre é
exatamente a mesma, e ela está repartida em **seis vezes mais pedaços**, com
sessenta por cento mais pedidos recusados.

É o esquecimento mais comum do assunto, e ele não aparece em nenhum teste de
"alocou e liberou sem vazar".

## O sistema de companheiros

```
$ dotnet medidor.dll buddy

  carga             pedido       ocupado   desperdicio   maior livre
  uniforme          53,376        53,376         0.0 %       524,288
  variada          176,326       238,524        26.1 %       524,288
  cauda longa      335,886       468,073        28.2 %       524,288
  alternada         80,000       135,168        40.8 %       524,288
```

Toda a memória é um bloco de 2^n, e um pedido menor parte o bloco ao meio até
chegar na potência de dois que cabe. As duas metades de um corte são
**companheiras**, e achar o companheiro de um bloco é um ou-exclusivo com o
tamanho: é isso que torna a junção imediata, sem procurar nada numa lista.

O preço está na coluna do desperdício:

```
  pedido   ocupado   desperdicio
      33        64        48.4 %
      64        64         0.0 %
      65       128        49.2 %
   1,025     2,048        50.0 %
```

É **fragmentação interna**: o bloco está ocupado, a lista de livres não sabe que
sobrou espaço dentro dele, e nenhum pedido novo vai caber ali. No pior caso ele
gasta quase o dobro, e o pior caso acontece sempre que o pedido passa de uma
potência de dois por um byte.

## O que as medidas me corrigiram

**A escolha do buraco quase não importa, e eu montei o repositório inteiro
esperando que importasse.** A enumeração de todas as escolhas possíveis bate as
estratégias por centésimos de recusa:

```
  passos   sequencias     otimo   primeiro encaixe   melhor encaixe   pior encaixe
       6          200      1.04               1.05             1.04           1.05
      10          200      2.37               2.38             2.38           2.38
```

Um alocador que pudesse ver o futuro e escolher o buraco perfeito em cada pedido
recusaria 2,37 pedidos em média onde o primeiro encaixe recusa 2,38. **O que
importa de verdade não é qual buraco escolher: é a junção, e é o tamanho dos
pedidos.**

**A minha carga de fragmentação não fragmentava.** A primeira versão pedia um
pequeno, pedia um grande e devolvia o pequeno, repetidamente. Ela não recusava um
pedido sequer, e o motivo é óbvio depois de escrito: o buraco deixado pelo pequeno
é reusado pelo pequeno seguinte. A ordem importa: para fragmentar, as devoluções
precisam vir **todas depois** das alocações.

**A conta de fragmentação interna do sistema de companheiros deu negativa.** Eu
somava todo pedido que já tinha acontecido e descontava do ocupado só o que estava
vivo. A fração desperdiçada saiu em -189,8%, -149,5% e -125,2% em três das quatro
cargas, e a tabela inteira foi impressa sem ninguém estranhar o sinal. O número
tem que ser do que está vivo **agora**, dos dois lados.

**Copiar a memória por realocação era muito mais difícil do que copiar os
blocos.** A primeira versão da cópia que a enumeração usa reconstruía a memória
alocando buraco por buraco, e precisava inventar donos falsos para os pedaços de
antes. Copiar a lista de blocos é uma linha e não tem o que dar errado.

## Por que o juiz são duas coisas

A contabilidade é barata e roda depois de **cada** operação: a fita cobre a
memória inteira em ordem, sem buraco e sem sobreposição; não há dois buracos
livres grudados; cada dono aparece uma vez; e o que o alocador acha que entregou
bate com o que está na fita. Conferir só no fim deixa passar um estado quebrado no
meio que a operação seguinte conserta por acaso.

E as conferências são testadas contra memórias estragadas de propósito: devolver
sem juntar, um dono repetido, um bloco a menos na fita. Se elas não pegassem esses
casos, não estariam conferindo nada.

A enumeração é exponencial e só cabe em sequências de dez passos, e é exatamente
o tamanho em que uma estratégia ruim ainda parece razoável. Ela é o que permite
dizer que uma estratégia desperdiçou, em vez de dizer que ela desperdiçou mais que
a outra.

O sorteio é de um gerador congruente escrito aqui, e não do sorteio da
biblioteca, cuja documentação não promete a mesma sequência entre versões nem
entre sistemas.

Nenhuma medida aqui usa relógio. Todas são bytes, buracos, recusas ou buracos
olhados por pedido.

## As peças

| arquivo | o que faz |
| --- | --- |
| `Memoria.cs` | a fita de bytes, o corte do buraco e a junção dos vizinhos |
| `Estrategias.cs` | as quatro escolhas de buraco e a contabilidade do alocador |
| `Buddy.cs` | o sistema de companheiros e a fragmentação interna |
| `Juiz.cs` | a contabilidade e a enumeração de todas as escolhas |
| `Medidas.cs` | as cargas e as tabelas que os testes conferem |

## Como rodar

```
dotnet test testes/Alocacao.Testes/Alocacao.Testes.csproj -c Release
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- tudo
```

As medidas aceitam `juiz`, `estrategias`, `juncao`, `buddy` e `tudo`.

## Licença

MIT.
