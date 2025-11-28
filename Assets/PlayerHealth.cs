using UnityEngine;
using UnityEngine.UI;

/*
GameObject: PlayerHealth (attach to Character prefab)
Descripción: Gestiona la barra de vida en espacio mundial, el daño y notifica muerte al manager.
*/

[RequireComponent(typeof(Transform))]
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;
    public Vector3 offset = new Vector3(0f, 2f, 0f);

    public float barWorldWidth = 0.6f;
    public float barWorldHeight = 0.08f;
    public Color barColor = Color.green;
    public Color backgroundColor = Color.red;
    public Color borderColor = Color.black;
    public float borderThickness = 0.01f;

    private RectTransform _canvasRect;
    private RectTransform _foregroundRect;
    private GameObject _canvasGO;

    // Awake: inicializa valores de vida y crea el canvas en mundo.
    void Awake()
    {
        currentHealth = maxHealth;
        CreateWorldCanvas();
        UpdateBarImmediate();
    }

    // TakeDamage: aplica daño y actualiza barra; comprueba muerte.
    // Parámetros: amount - cantidad de daño a aplicar.
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateBarImmediate();
        CheckForDeath();
    }

    // CreateWorldCanvas: construye una pequeña UI en world space como hijo del jugador.
    private void CreateWorldCanvas()
    {
        _canvasGO = new GameObject("HealthCanvas", typeof(RectTransform));
        _canvasGO.transform.SetParent(transform, false);

        Vector3 parentScale = transform.lossyScale;
        Vector3 invScale = new Vector3(
            parentScale.x != 0f ? 1f / parentScale.x : 1f,
            parentScale.y != 0f ? 1f / parentScale.y : 1f,
            parentScale.z != 0f ? 1f / parentScale.z : 1f
        );

        _canvasGO.transform.localPosition = new Vector3(offset.x * invScale.x, offset.y * invScale.y, offset.z * invScale.z);
        _canvasGO.transform.localRotation = Quaternion.identity;

        var canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;
        canvas.overrideSorting = false;

        var scaler = _canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;

        _canvasGO.AddComponent<GraphicRaycaster>();

        _canvasRect = _canvasGO.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = new Vector2(barWorldWidth, barWorldHeight);

        _canvasRect.localScale = invScale;

        var bgGO = new GameObject("BG", typeof(Image));
        bgGO.transform.SetParent(_canvasGO.transform, false);
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = borderColor;
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0f);
        bgRt.anchorMax = new Vector2(1f, 1f);
        bgRt.sizeDelta = new Vector2(0f, 0f);

        var innerGO = new GameObject("Inner", typeof(Image));
        innerGO.transform.SetParent(bgGO.transform, false);
        var innerImg = innerGO.GetComponent<Image>();
        innerImg.color = backgroundColor;
        var innerRt = innerGO.GetComponent<RectTransform>();
        float bt = borderThickness;
        innerRt.anchorMin = new Vector2(0f, 0f);
        innerRt.anchorMax = new Vector2(1f, 1f);
        innerRt.sizeDelta = new Vector2(-bt * 2f, -bt * 2f);

        var fgGO = new GameObject("Foreground", typeof(Image));
        fgGO.transform.SetParent(innerGO.transform, false);
        var fgImg = fgGO.GetComponent<Image>();
        fgImg.color = barColor;
        _foregroundRect = fgGO.GetComponent<RectTransform>();
        _foregroundRect.anchorMin = new Vector2(0f, 0f);
        _foregroundRect.anchorMax = new Vector2(0f, 1f);
        _foregroundRect.pivot = new Vector2(0f, 0.5f);
        _foregroundRect.sizeDelta = new Vector2(barWorldWidth - bt * 2f, barWorldHeight - bt * 2f);
        _foregroundRect.anchoredPosition = new Vector2(bt, 0f);
    }

    // UpdateBarImmediate: actualiza el ancho de la barra según la vida actual.
    private void UpdateBarImmediate()
    {
        if (_foregroundRect == null || maxHealth <= 0) return;
        float ratio = Mathf.Clamp01((float)currentHealth / maxHealth);
        float innerWidth = barWorldWidth - borderThickness * 2f;
        float newWidth = innerWidth * ratio;
        _foregroundRect.sizeDelta = new Vector2(newWidth, barWorldHeight - borderThickness * 2f);
    }

    // OnWillRenderObject: orienta el canvas hacia la cámara que está renderizando.
    private void OnWillRenderObject()
    {
        var cam = Camera.current;
        if (cam == null || _canvasGO == null) return;

        Vector3 dir = _canvasGO.transform.position - cam.transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        _canvasGO.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    // SetHealth: asigna vida y actualiza estado.
    // Parámetros: value - nueva vida.
    public void SetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        UpdateBarImmediate();
        CheckForDeath();
    }

    // CheckForDeath: notifica al Character/manager si la vida llega a 0.
    private void CheckForDeath()
    {
        if (currentHealth <= 0)
        {
            var ch = GetComponent<Character>();
            if (ch != null && ch.manager != null)
            {
                ch.manager.PlayerLost(ch);
            }
        }
    }

    // OnDestroy: destruye el canvas creado para evitar fugas.
    private void OnDestroy()
    {
        if (_canvasGO != null) Destroy(_canvasGO);
    }
}
