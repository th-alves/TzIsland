namespace TzIsland;

/// <summary>Estilo da animação de abrir/fechar a ilha (escolhido em Configurações → Efeitos e Fluidez).</summary>
public enum IslandAnim { Fluid, Elastic, Crystal, Precise, Bubble }

/// <summary>
/// Conjunto de parâmetros de mola de um estilo. Cada par K/C é (rigidez, amortecimento): amortecimento crítico = 2·√K;
/// abaixo disso a mola "passa do ponto" e volta (efeito elástico); acima, chega devagar sem quicar.
/// </summary>
public sealed class AnimProfile
{
    public string Name = "", Hint = "";

    // Expandir — forma (largura, altura, raio dos cantos)
    public double WK, WC, HK, HC, RK, RC, HDelay, RDelay;
    // Expandir — conteúdo: opacidade, escala e subida
    public double ODelay, OK, OC;
    public double SStart = 0.94, SDelay, SK, SC;
    public double YStart = 14, YDelay, YK, YC;
    // Expandir — entrada em cascata dos cartões
    public double RevealBase, RevealStep, RevealY = 18, RevealScale = 0.94;
    // Recolher
    public double CK, CC, CShapeDelay, COK, COC, SOut = 0.96, CSK, CSC, YOut = 8, MiniDelay;
}

public static class AnimProfiles
{
    public static readonly AnimProfile Fluid = new()
    {
        Name = "Fluida", Hint = "O padrão: a forma se abre em molas diferentes (morph) e os cartões entram em cascata.",
        WK = 320, WC = 24, HK = 250, HC = 25, RK = 260, RC = 26,
        ODelay = 0.07, OK = 360, OC = 36, SStart = 0.94, SDelay = 0.03, SK = 240, SC = 17, YStart = 14, YDelay = 0.03, YK = 240, YC = 20,
        RevealBase = 0.10, RevealStep = 0.055, RevealY = 18, RevealScale = 0.94,
        CK = 340, CC = 30, CShapeDelay = 0.05, COK = 700, COC = 52, SOut = 0.96, CSK = 420, CSC = 34, YOut = 8, MiniDelay = 0.12
    };

    public static readonly AnimProfile Elastic = new()
    {
        Name = "Elástica", Hint = "Passa um pouco do tamanho e volta, como uma mola de verdade (estilo Alcove). Os cartões \"pipocam\".",
        WK = 250, WC = 17.4, HK = 220, HC = 17, RK = 240, RC = 17, HDelay = 0.02,
        ODelay = 0.06, OK = 320, OC = 30, SStart = 0.88, SDelay = 0.02, SK = 200, SC = 12, YStart = 22, YDelay = 0.02, YK = 200, YC = 15,
        RevealBase = 0.09, RevealStep = 0.065, RevealY = 26, RevealScale = 0.86,
        CK = 300, CC = 20, CShapeDelay = 0.04, COK = 700, COC = 52, SOut = 0.94, CSK = 380, CSC = 26, YOut = 10, MiniDelay = 0.10
    };

    public static readonly AnimProfile Crystal = new()
    {
        Name = "Cristal", Hint = "Desliza devagar e sem quicar, com o conteúdo aparecendo só depois que a forma se assenta. Elegante e calma.",
        WK = 150, WC = 24.5, HK = 130, HC = 22.8, RK = 140, RC = 23.7,
        ODelay = 0.16, OK = 160, OC = 25, SStart = 0.975, SDelay = 0.10, SK = 150, SC = 24, YStart = 10, YDelay = 0.10, YK = 150, YC = 24,
        RevealBase = 0.20, RevealStep = 0.045, RevealY = 10, RevealScale = 0.975,
        CK = 230, CC = 30, CShapeDelay = 0.06, COK = 400, COC = 40, SOut = 0.985, CSK = 300, CSC = 34, YOut = 6, MiniDelay = 0.16
    };

    public static readonly AnimProfile Precise = new()
    {
        Name = "Precisa", Hint = "Rápida e seca, sem nenhuma sobra: abre e fecha quase na hora, com a firmeza de um app nativo.",
        WK = 520, WC = 45.6, HK = 520, HC = 45.6, RK = 520, RC = 45.6,
        ODelay = 0.02, OK = 700, OC = 52, SStart = 0.985, SDelay = 0, SK = 520, SC = 45, YStart = 6, YDelay = 0, YK = 520, YC = 45,
        RevealBase = 0.04, RevealStep = 0.02, RevealY = 6, RevealScale = 0.985,
        CK = 640, CC = 50, CShapeDelay = 0, COK = 900, COC = 60, SOut = 0.99, CSK = 700, CSC = 52, YOut = 4, MiniDelay = 0.0
    };

    public static readonly AnimProfile Bubble = new()
    {
        Name = "Bolha", Hint = "Cresce como uma bolha e o conteúdo surge de dentro, do centro para fora, cartão por cartão.",
        WK = 190, WC = 14.3, HK = 190, HC = 14.3, RK = 120, RC = 13,
        ODelay = 0.10, OK = 260, OC = 28, SStart = 0.72, SDelay = 0.05, SK = 170, SC = 11, YStart = 0, YDelay = 0, YK = 200, YC = 20,
        RevealBase = 0.12, RevealStep = 0.05, RevealY = 0, RevealScale = 0.7,
        CK = 260, CC = 19, CShapeDelay = 0.03, COK = 700, COC = 52, SOut = 0.8, CSK = 380, CSC = 26, YOut = 0, MiniDelay = 0.10
    };

    public static AnimProfile Get(IslandAnim a) => a switch
    {
        IslandAnim.Elastic => Elastic, IslandAnim.Crystal => Crystal, IslandAnim.Precise => Precise, IslandAnim.Bubble => Bubble, _ => Fluid
    };

    public static readonly (string, IslandAnim)[] All =
    {
        (Fluid.Name, IslandAnim.Fluid), (Elastic.Name, IslandAnim.Elastic), (Crystal.Name, IslandAnim.Crystal),
        (Precise.Name, IslandAnim.Precise), (Bubble.Name, IslandAnim.Bubble)
    };
}
