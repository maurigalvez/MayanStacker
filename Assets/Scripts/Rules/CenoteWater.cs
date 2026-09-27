using UnityEngine;

/// <summary>
/// Flat vector-art water for <see cref="RisingCenote"/>, built as runtime meshes — no texture.
///
/// Layers, back to front:
///  - a pale back wave that peeks over the front surface, on its own slower phase;
///  - the water body in three flat turquoise bands that deepen with depth, gently wavy edges;
///  - a cream crest line on the surface with a dark edge above it (see <see cref="EffectOutline"/>),
///    a few drifting foam dashes just under it, and bubbles rising from the deep.
///
/// Everything is sized in stone heights so it reads the same on any stone scale, uses world x
/// so the waves don't slide when the camera moves, and runs on scaled time (pauses with the game).
/// Presentation only — cosmetic randomness here never touches the seeded run stream.
/// </summary>
public class CenoteWater : MonoBehaviour
{
    private const int Columns = 56;
    private const int Dashes = 5;
    private const int DashSegments = 8;
    private const int Bubbles = 7;
    private const int BubbleRim = 10;

    // Shape, in stone heights.
    private const float Wavelength = 1.6f;
    private const float FrontAmplitude = 0.07f;
    private const float BackAmplitude = 0.08f;
    private const float BackLift = 0.09f;
    private const float CrestWidth = 0.05f;
    private const float EdgeWidth = 0.035f;
    private const float Band1Depth = 0.9f;
    private const float Band2Depth = 2.2f;

    private static readonly Color BackColor  = new Color(0.40f, 0.80f, 0.76f, 0.55f);
    private static readonly Color Band0Color = new Color(0.16f, 0.62f, 0.64f, 0.55f);
    private static readonly Color Band1Color = new Color(0.09f, 0.47f, 0.55f, 0.60f);
    private static readonly Color Band2Color = new Color(0.05f, 0.33f, 0.45f, 0.66f);
    private static readonly Color CrestColor = new Color(0.88f, 0.98f, 0.93f, 0.95f);
    private static readonly Color FoamColor  = new Color(0.80f, 0.96f, 0.92f, 0.55f);

    private Mesh backMesh, bodyMesh, glintMesh;
    private MeshRenderer backRenderer, bodyRenderer, glintRenderer;

    private Vector3[] backV;  private Color[] backC;
    private Vector3[] bodyV;  private Color[] bodyC;
    private Vector3[] glintV; private Color[] glintC;

    private float unit; // smoothed stone height, so a differently sized stone doesn't jump the waves
    private readonly float[] surfaceY = new float[Columns];
    private readonly float[] columnX = new float[Columns];

    private readonly float[] dashX = new float[Dashes];
    private readonly float[] dashLen = new float[Dashes];
    private readonly float[] dashDepth = new float[Dashes];
    private readonly float[] dashSpeed = new float[Dashes];

    private readonly float[] bubbleX = new float[Bubbles];
    private readonly float[] bubbleAge = new float[Bubbles];
    private readonly float[] bubbleLife = new float[Bubbles];
    private readonly float[] bubbleSize = new float[Bubbles];
    private readonly float[] bubbleDepth = new float[Bubbles];

    // Glint mesh layout: surface edge, crest, dashes, bubbles.
    private int edgeStart, crestStart, dashStart, bubbleStart;

    public void Build(int sortingOrder)
    {
        var material = new Material(Shader.Find("Sprites/Default"));

        backMesh = CreateLayer("CenoteBackWave", material, sortingOrder, out backRenderer);
        bodyMesh = CreateLayer("CenoteBody", material, sortingOrder + 1, out bodyRenderer);
        glintMesh = CreateLayer("CenoteGlints", material, sortingOrder + 2, out glintRenderer);

        // Back wave: a strip (top edge + bottom edge + outline over the top).
        backV = new Vector3[Columns * 4];
        backC = new Color[backV.Length];
        var backT = new int[(Columns - 1) * 12];
        int t = 0;
        for (int i = 0; i < Columns - 1; i++)
        {
            t = Quad(backT, t, i * 2, (i + 1) * 2, 1);                          // fill
            t = Quad(backT, t, Columns * 2 + i * 2, Columns * 2 + (i + 1) * 2, 1); // outline
        }
        backMesh.vertices = backV;
        backMesh.triangles = backT;

        // Body: three bands, each its own strip so the colours stay flat.
        bodyV = new Vector3[Columns * 2 * 3];
        bodyC = new Color[bodyV.Length];
        var bodyT = new int[(Columns - 1) * 6 * 3];
        t = 0;
        for (int b = 0; b < 3; b++)
            for (int i = 0; i < Columns - 1; i++)
                t = Quad(bodyT, t, b * Columns * 2 + i * 2, b * Columns * 2 + (i + 1) * 2, 1);
        bodyMesh.vertices = bodyV;
        bodyMesh.triangles = bodyT;

        // Glints.
        edgeStart = 0;
        crestStart = Columns * 2;
        dashStart = crestStart + Columns * 2;
        bubbleStart = dashStart + Dashes * (DashSegments + 1) * 2;
        glintV = new Vector3[bubbleStart + Bubbles * (BubbleRim + 1)];
        glintC = new Color[glintV.Length];

        var glintT = new int[(Columns - 1) * 6 * 2 + Dashes * DashSegments * 6 + Bubbles * BubbleRim * 3];
        t = 0;
        for (int i = 0; i < Columns - 1; i++)
        {
            t = Quad(glintT, t, edgeStart + i * 2, edgeStart + (i + 1) * 2, 1);
            t = Quad(glintT, t, crestStart + i * 2, crestStart + (i + 1) * 2, 1);
        }
        for (int d = 0; d < Dashes; d++)
        {
            int s0 = dashStart + d * (DashSegments + 1) * 2;
            for (int s = 0; s < DashSegments; s++) t = Quad(glintT, t, s0 + s * 2, s0 + (s + 1) * 2, 1);
        }
        for (int b = 0; b < Bubbles; b++)
        {
            int c = bubbleStart + b * (BubbleRim + 1);
            for (int r = 0; r < BubbleRim; r++)
            {
                glintT[t++] = c;
                glintT[t++] = c + 1 + (r + 1) % BubbleRim;
                glintT[t++] = c + 1 + r;
            }
        }
        glintMesh.vertices = glintV;
        glintMesh.triangles = glintT;

        for (int d = 0; d < Dashes; d++) SeedDash(d, Random.Range(-6f, 6f), d % 2 == 0 ? 1f : -1f);
        for (int b = 0; b < Bubbles; b++) { SeedBubble(b, 0f); bubbleAge[b] = Random.Range(0f, bubbleLife[b]); }

        SetShown(false);
    }

    public void SetShown(bool shown)
    {
        if (backRenderer == null || backRenderer.gameObject.activeSelf == shown) return;
        backRenderer.gameObject.SetActive(shown);
        bodyRenderer.gameObject.SetActive(shown);
        glintRenderer.gameObject.SetActive(shown);
    }

    /// <summary>
    /// Redraws the water. <paramref name="danger"/> 0..1 tints it toward <paramref name="warning"/>;
    /// <paramref name="alpha"/> fades the whole thing in.
    /// </summary>
    public void Draw(float centreX, float halfWidth, float waterY, float bottomY, float stone,
                     float danger, Color warning, float alpha)
    {
        if (bodyMesh == null) return;

        // Meshes are in world space.
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        unit = unit <= 0f ? stone : Mathf.Lerp(unit, stone, 1f - Mathf.Exp(-2f * Time.deltaTime));
        stone = unit;
        float time = Time.time;
        float k = Mathf.PI * 2f / (Wavelength * stone);
        float left = centreX - halfWidth;
        float step = halfWidth * 2f / (Columns - 1);
        float bob = Mathf.Sin(time * 1.4f) * 0.02f * stone;

        for (int i = 0; i < Columns; i++)
        {
            float x = left + step * i;
            columnX[i] = x;
            surfaceY[i] = waterY + bob + FrontAmplitude * stone *
                          (0.65f * Mathf.Sin(x * k + time * 1.2f) + 0.35f * Mathf.Sin(x * k * 1.9f - time * 1.7f + 1.3f));
        }

        Color back = Tint(BackColor, warning, danger * 0.6f, alpha);
        Color edge = EffectOutline.Color; edge.a *= 0.8f * alpha;
        Color band0 = Tint(Band0Color, warning, danger * 0.7f, alpha);
        Color band1 = Tint(Band1Color, warning, danger * 0.5f, alpha);
        Color band2 = Tint(Band2Color, warning, danger * 0.35f, alpha);
        Color crest = Tint(CrestColor, warning, danger * 0.3f, alpha);
        Color foam = Tint(FoamColor, warning, danger * 0.3f, alpha);

        // Back wave: slower, longer, lifted; its strip ends just under the front troughs.
        float backBottom = waterY + bob - FrontAmplitude * stone * 1.2f;
        for (int i = 0; i < Columns; i++)
        {
            float x = columnX[i];
            float y = waterY + bob + BackLift * stone +
                      BackAmplitude * stone * Mathf.Sin(x * k * 0.75f - time * 0.9f + 2.1f);
            SetPair(backV, backC, i * 2, x, y, backBottom, back);
            SetPair(backV, backC, Columns * 2 + i * 2, x, y + EdgeWidth * 0.7f * stone, y, edge);
        }

        // Body bands, each clamped to the drawn water.
        int o1 = Columns * 2, o2 = Columns * 4;
        for (int i = 0; i < Columns; i++)
        {
            float x = columnX[i];
            float top = surfaceY[i];
            float d1 = Mathf.Clamp(waterY - Band1Depth * stone + 0.05f * stone * Mathf.Sin(x * k * 0.6f + time * 0.5f), bottomY, top);
            float d2 = Mathf.Clamp(waterY - Band2Depth * stone + 0.06f * stone * Mathf.Sin(x * k * 0.45f - time * 0.4f + 0.8f), bottomY, d1);
            SetPair(bodyV, bodyC, i * 2, x, top, d1, band0);
            SetPair(bodyV, bodyC, o1 + i * 2, x, d1, d2, band1);
            SetPair(bodyV, bodyC, o2 + i * 2, x, d2, bottomY, band2);
        }

        // Dark edge above the surface, cream crest just under it.
        for (int i = 0; i < Columns; i++)
        {
            float x = columnX[i];
            float y = surfaceY[i];
            SetPair(glintV, glintC, edgeStart + i * 2, x, y + EdgeWidth * stone, y, edge);
            SetPair(glintV, glintC, crestStart + i * 2, x, y, y - CrestWidth * stone, crest);
        }

        float dt = Time.deltaTime;
        for (int d = 0; d < Dashes; d++) DrawDash(d, left, halfWidth * 2f, stone, k, time, dt, foam);
        for (int b = 0; b < Bubbles; b++) DrawBubble(b, left, halfWidth * 2f, waterY, bottomY, stone, dt, foam);

        Apply(backMesh, backV, backC);
        Apply(bodyMesh, bodyV, bodyC);
        Apply(glintMesh, glintV, glintC);
    }

    private void DrawDash(int d, float left, float width, float stone, float k, float time, float dt, Color foam)
    {
        dashX[d] += dashSpeed[d] * stone * dt;
        float span = dashLen[d] * stone;
        if (dashX[d] > left + width + span || dashX[d] < left - span * 2f)
            SeedDash(d, dashSpeed[d] > 0f ? left - span : left + width + span, Mathf.Sign(dashSpeed[d]));

        int s0 = dashStart + d * (DashSegments + 1) * 2;
        float thick = 0.03f * stone;
        for (int s = 0; s <= DashSegments; s++)
        {
            float f = s / (float)DashSegments;
            float x = dashX[d] + span * f;
            float y = SurfaceAt(x, left, width) - dashDepth[d] * stone;
            float taper = Mathf.Sin(f * Mathf.PI); // pointed ends
            SetPair(glintV, glintC, s0 + s * 2, x, y + thick * taper * 0.5f, y - thick * taper * 0.5f, foam);
        }
    }

    private void DrawBubble(int b, float left, float width, float waterY, float bottomY, float stone, float dt, Color foam)
    {
        bubbleAge[b] += dt;
        if (bubbleAge[b] >= bubbleLife[b]) SeedBubble(b, 0f);

        float f = bubbleAge[b] / bubbleLife[b];
        float x = left + bubbleX[b] * width + Mathf.Sin((bubbleAge[b] + b) * 3f) * 0.04f * stone;
        float y = Mathf.Max(bottomY, waterY - bubbleDepth[b] * stone * (1f - f)) - 0.12f * stone;
        float r = bubbleSize[b] * stone * Mathf.Lerp(0.6f, 1f, f);
        Color c = foam;
        c.a *= Mathf.Sin(f * Mathf.PI) * 0.8f;

        int centre = bubbleStart + b * (BubbleRim + 1);
        glintV[centre] = new Vector3(x, y, 0f);
        glintC[centre] = c;
        for (int i = 0; i < BubbleRim; i++)
        {
            float a = i * Mathf.PI * 2f / BubbleRim;
            glintV[centre + 1 + i] = new Vector3(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r, 0f);
            glintC[centre + 1 + i] = c;
        }
    }

    private float SurfaceAt(float x, float left, float width)
    {
        float f = Mathf.Clamp01((x - left) / width) * (Columns - 1);
        int i = Mathf.Min(Columns - 2, (int)f);
        return Mathf.Lerp(surfaceY[i], surfaceY[i + 1], f - i);
    }

    private void SeedDash(int d, float x, float direction)
    {
        dashX[d] = x;
        dashLen[d] = Random.Range(0.35f, 0.8f);
        dashDepth[d] = Random.Range(0.14f, 0.38f);
        dashSpeed[d] = Random.Range(0.12f, 0.3f) * direction;
    }

    private void SeedBubble(int b, float age)
    {
        bubbleX[b] = Random.Range(0.05f, 0.95f);
        bubbleAge[b] = age;
        bubbleLife[b] = Random.Range(2.2f, 4f);
        bubbleSize[b] = Random.Range(0.035f, 0.07f);
        bubbleDepth[b] = Random.Range(1.2f, 2.6f);
    }

    private static Color Tint(Color c, Color warning, float amount, float alpha)
    {
        float a = c.a * alpha;
        c = Color.Lerp(c, warning, amount);
        c.a = a;
        return c;
    }

    private static void SetPair(Vector3[] v, Color[] c, int i, float x, float top, float bottom, Color color)
    {
        v[i] = new Vector3(x, top, 0f);
        v[i + 1] = new Vector3(x, bottom, 0f);
        c[i] = color;
        c[i + 1] = color;
    }

    /// <summary>Two triangles between column pairs (a, a+1) and (b, b+1).</summary>
    private static int Quad(int[] tris, int t, int a, int b, int down)
    {
        tris[t++] = a; tris[t++] = b; tris[t++] = a + down;
        tris[t++] = b; tris[t++] = b + down; tris[t++] = a + down;
        return t;
    }

    private static void Apply(Mesh mesh, Vector3[] v, Color[] c)
    {
        mesh.vertices = v;
        mesh.colors = c;
        mesh.RecalculateBounds();
    }

    private Mesh CreateLayer(string name, Material material, int order, out MeshRenderer renderer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return mesh;
    }
}
