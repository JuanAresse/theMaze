using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Transform))]
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;
    public Vector3 offset = new Vector3(0f, 2f, 0f);

    // Apariencia (metros)
    public float barWorldWidth = 0.6f;
    public float barWorldHeight = 0.08f;
    public Color barColor = Color.green;
    public Color backgroundColor = Color.red;
    public Color borderColor = Color.black;
    public float borderThickness = 0.01f;

    // Referencias internas
    private RectTransform _canvasRect;
    private RectTransform _foregroundRect;
    private GameObject _canvasGO;

    void Awake()
    {
        currentHealth = maxHealth;
        CreateWorldCanvas();
        UpdateBarImmediate();
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateBarImmediate();
        CheckForDeath();
    }

    private void CreateWorldCanvas()
    {
        // Root canvas object (child del jugador)
        _canvasGO = new GameObject("HealthCanvas", typeof(RectTransform));
        _canvasGO.transform.SetParent(transform, false);

        // Neutralizar la escala heredada: calculamos la escala del padre y aplicamos la inversa
        Vector3 parentScale = transform.lossyScale;
        Vector3 invScale = new Vector3(
            parentScale.x != 0f ? 1f / parentScale.x : 1f,
            parentScale.y != 0f ? 1f / parentScale.y : 1f,
            parentScale.z != 0f ? 1f / parentScale.z : 1f
        );

        // Ajustar la posición local para que 'offset' represente unidades del mundo
        _canvasGO.transform.localPosition = new Vector3(offset.x * invScale.x, offset.y * invScale.y, offset.z * invScale.z);
        _canvasGO.transform.localRotation = Quaternion.identity;

        // Canvas (World Space)
        var canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null; // se orienta por cámara en OnWillRenderObject
        canvas.overrideSorting = false;

        var scaler = _canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;

        _canvasGO.AddComponent<GraphicRaycaster>();

        _canvasRect = _canvasGO.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = new Vector2(barWorldWidth, barWorldHeight);

        // Aplicar la escala inversa para neutralizar la escala del padre (el canvas tendrá tamaño en unidades del mundo)
        _canvasRect.localScale = invScale;

        // Background (border)
        var bgGO = new GameObject("BG", typeof(Image));
        bgGO.transform.SetParent(_canvasGO.transform, false);
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = borderColor;
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0f);
        bgRt.anchorMax = new Vector2(1f, 1f);
        bgRt.sizeDelta = new Vector2(0f, 0f);

        // Inner background (life full)
        var innerGO = new GameObject("Inner", typeof(Image));
        innerGO.transform.SetParent(bgGO.transform, false);
        var innerImg = innerGO.GetComponent<Image>();
        innerImg.color = backgroundColor;
        var innerRt = innerGO.GetComponent<RectTransform>();
        float bt = borderThickness;
        innerRt.anchorMin = new Vector2(0f, 0f);
        innerRt.anchorMax = new Vector2(1f, 1f);
        innerRt.sizeDelta = new Vector2(-bt * 2f, -bt * 2f);

        // Foreground (actual life)
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

    private void UpdateBarImmediate()
    {
        if (_foregroundRect == null || maxHealth <= 0) return;
        float ratio = Mathf.Clamp01((float)currentHealth / maxHealth);
        float innerWidth = barWorldWidth - borderThickness * 2f;
        float newWidth = innerWidth * ratio;
        _foregroundRect.sizeDelta = new Vector2(newWidth, barWorldHeight - borderThickness * 2f);
    }

    // OnWillRenderObject es llamado antes de que el object sea renderizado por una cámara.
    // Usamos Camera.current para orientar la canvas hacia la cámara que está renderizando.
    private void OnWillRenderObject()
    {
        var cam = Camera.current;
        if (cam == null || _canvasGO == null) return;

        Vector3 dir = _canvasGO.transform.position - cam.transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        _canvasGO.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    public void SetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        UpdateBarImmediate();
        CheckForDeath();
    }

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

    private void OnDestroy()
    {
        if (_canvasGO != null) Destroy(_canvasGO);
    }
}
