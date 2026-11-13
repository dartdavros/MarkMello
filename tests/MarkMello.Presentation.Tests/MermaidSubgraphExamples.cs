namespace MarkMello.Presentation.Tests;

internal static class MermaidSubgraphExamples
{
    public const string NestedDirections =
        """
        flowchart LR
            subgraph TOP
                direction TB
                subgraph B1
                    direction RL
                    i1 --> f1
                end
                subgraph B2
                    direction BT
                    i2 --> f2
                end
                B1 --> B2
            end
            A --> TOP --> B
        """;

    public static readonly string[] Issue48Labels =
    [
        "Модель OSI (7 уровней)", "Стек TCP/IP (4 уровня)",
        "7. Прикладной", "6. Представления", "5. Сеансовый", "4. Транспортный",
        "3. Сетевой", "2. Канальный", "1. Физический", "Прикладной", "Транспортный",
        "Межсетевой", "Доступа к среде"
    ];

    public const string Issue48 =
        """
        flowchart LR
            subgraph OSI["Модель OSI (7 уровней)"]
                direction TB
                O7["7. Прикладной"]
                O6["6. Представления"]
                O5["5. Сеансовый"]
                O4["4. Транспортный"]
                O3["3. Сетевой"]
                O2["2. Канальный"]
                O1["1. Физический"]
            end
            subgraph TCPIP["Стек TCP/IP (4 уровня)"]
                direction TB
                T4["Прикладной"]
                T3["Транспортный"]
                T2["Межсетевой"]
                T1["Доступа к среде"]
            end
            O7 --- T4
            O6 --- T4
            O5 --- T4
            O4 --- T3
            O3 --- T2
            O2 --- T1
            O1 --- T1
            classDef osi fill:#dbeafe,stroke:#1e40af
            classDef tcp fill:#dcfce7,stroke:#166534
            class O1,O2,O3,O4,O5,O6,O7 osi
            class T1,T2,T3,T4 tcp
        """;
}
