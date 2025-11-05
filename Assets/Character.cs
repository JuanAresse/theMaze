using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    public enum Facing { North, South, East, West }

    private MazeCell[,] _grid;
    private int _width;
    private int _depth;
    private Vector2Int _pos;
    private string _name;

    public TurnBasedManager manager;
    public PlayerHealth health; // Referencia a su PlayerHealth

    [SerializeField] private float fixedHeight = 0.5f;

    private Rigidbody _rb;
    private Collider _collider;
    private bool _logicLocked = false;

    public Facing CurrentFacing { get; private set; } = Facing.North;

    // Visual model (solo rota el visual, no el root)
    private Transform _visual;
    [SerializeField] private float visualRotateSpeed = 10f;
    private Quaternion _targetVisualLocalRot = Quaternion.identity;

    // Offset local del visual (ajusta en el Inspector si el modelo queda dentro del cubo)
    [SerializeField] private Vector3 _visualLocalOffset = new Vector3(0f, 0.5f, 0f);

    // Nueva referencia pública para que MazeGenerator le asigne la cámara correspondiente
    public CamaraTerceraPersona cameraController;

    public void Init(MazeCell[,] grid, Vector2Int startPos, string playerName)
    {
        _grid = grid;
        _width = grid.GetLength(0);
        _depth = grid.GetLength(1);
        _pos = startPos;
        _name = playerName;

        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        if (_collider != null)
        {
            _collider.isTrigger = true;
        }

        Vector3 startWorld = new Vector3(_pos.x, fixedHeight, _pos.y);
        if (_rb != null) _rb.position = startWorld;
        else transform.position = startWorld;

        CurrentFacing = Facing.North;

        if (health == null)
        {
            health = GetComponent<PlayerHealth>();
            if (health == null) health = gameObject.AddComponent<PlayerHealth>();
        }
    }

    private void LateUpdate()
    {
        Vector3 lockedPos = new Vector3(_pos.x, fixedHeight, _pos.y);
        if (_rb != null) _rb.position = lockedPos;
        else transform.position = lockedPos;

        // Suavizar rotación del visual (si existe)
        if (_visual != null)
        {
            _visual.localRotation = Quaternion.Slerp(_visual.localRotation, _targetVisualLocalRot, Time.deltaTime * visualRotateSpeed);
        }
    }

    public IEnumerator ExecuteMoves(List<System.Action> moves, float stepDelay = 0.25f)
    {
        if (_grid == null) yield break;

        foreach (var action in moves)
        {
            if (_logicLocked) yield break;
            action.Invoke();    
            yield return new WaitForSeconds(stepDelay);
        }
    }

    // AttachVisual mejorado:
    // - crea/usa "VisualRoot" para neutralizar escala del prefab Character
    // - parenta el modelo bajo VisualRoot manteniendo la escala del modelo
    // - desactiva Renderers del prefab original (placeholder) sin desactivar los del modelo
    // - PRESERVA los Canvas creados por PlayerHealth (y cualquier canvas hijo del componente PlayerHealth)
    public void AttachVisual(GameObject visualInstance)
    {
        if (visualInstance == null) return;

        // Si nos pasan el prefab en vez de instancia, instanciamos
        GameObject instance = visualInstance;
        if (visualInstance.scene.rootCount == 0) // not in scene
        {
            instance = Instantiate(visualInstance);
        }

        // Crear o localizar VisualRoot
        Transform visualRoot = transform.Find("VisualRoot");
        if (visualRoot == null)
        {
            var vr = new GameObject("VisualRoot");
            vr.transform.SetParent(transform, false);
            vr.transform.localPosition = _visualLocalOffset;
            vr.transform.localRotation = Quaternion.identity;
            // Cancelar la escala local del Character para que el modelo no herede la escala del prefab
            Vector3 parentScale = transform.localScale;
            vr.transform.localScale = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f
            );
            visualRoot = vr.transform;
        }
        else
        {
            // Actualizar offset en caso de que se cambie en runtime
            visualRoot.localPosition = _visualLocalOffset;
            // Recalcular cancelación de escala por si se modificó la escala del padre
            Vector3 parentScale = transform.localScale;
            visualRoot.localScale = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f
            );
        }

        // Parentar la instancia dentro de VisualRoot (sin alterar su escala/posición relativa al VisualRoot)
        instance.transform.SetParent(visualRoot, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        _visual = instance.transform;
        _targetVisualLocalRot = _visual.localRotation;

        // Recolectamos todos los renderers bajo el Character y los del visual para diferenciarlos.
        var allRenderers = GetComponentsInChildren<Renderer>(true);
        var visualRenderers = _visual.GetComponentsInChildren<Renderer>(true);
        var visualRendererSet = new HashSet<Renderer>(visualRenderers);

        foreach (var r in allRenderers)
        {
            if (visualRendererSet.Contains(r)) continue; // es del modelo -> no tocar
            // Desactivar renderer del prefab/placeholder
            r.enabled = false;
        }

        // PRESERVAR Canvas que pertenecen a PlayerHealth:
        var healthCanvases = new HashSet<UnityEngine.Canvas>();
        var healthComps = GetComponentsInChildren<PlayerHealth>(true);
        foreach (var hp in healthComps)
        {
            var cs = hp.GetComponentsInChildren<UnityEngine.Canvas>(true);
            foreach (var c in cs) healthCanvases.Add(c);
        }

        // También desactivar cualquier Canvas en el prefab que no pertenezca al visual (si hay),
        // pero mantener los Canvas de PlayerHealth y los del visual.
        var allCanvases = GetComponentsInChildren<UnityEngine.Canvas>(true);
        var visualCanvases = _visual.GetComponentsInChildren<UnityEngine.Canvas>(true);
        var vcSet = new HashSet<UnityEngine.Canvas>(visualCanvases);

        foreach (var c in allCanvases)
        {
            if (vcSet.Contains(c)) continue;        // es del modelo -> no tocar
            if (healthCanvases.Contains(c)) continue; // es de PlayerHealth -> no tocar
            c.enabled = false;
        }
    }

    // Nuevo: teletransporta el personaje a la celda indicada y fuerza centrado inmediato
    public void TeleportTo(Vector2Int cellPos)
    {
        _pos = cellPos;
        Vector3 world = new Vector3(_pos.x, fixedHeight, _pos.y);
        if (_rb != null)
        {
            _rb.position = world;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.position = world;
        }
    }

    // Movement: set facing and when turning left/right, request visual rotation.
    public void Move(Vector2Int dir)
    {
        if (_grid == null) return;

        if (dir == Vector2Int.up) SetFacing(Facing.North, false);
        else if (dir == Vector2Int.down) SetFacing(Facing.South, false);
        else if (dir == Vector2Int.right) SetFacing(Facing.East, true);
        else if (dir == Vector2Int.left) SetFacing(Facing.West, true);

        var currentCell = _grid[_pos.x, _pos.y];

        if (dir == Vector2Int.right)
        {
            if (currentCell.HasRightWall()) return;
            if (_pos.x + 1 >= _width) return;
            var target = _grid[_pos.x + 1, _pos.y];
            if (target.HasLeftWall()) return;
            _pos.x += 1;
        }
        else if (dir == Vector2Int.left)
        {
            if (currentCell.HasLeftWall()) return;
            if (_pos.x - 1 < 0) return;
            var target = _grid[_pos.x - 1, _pos.y];
            if (target.HasRightWall()) return;
            _pos.x -= 1;
        }
        else if (dir == Vector2Int.up)
        {
            if (currentCell.HasFrontWall()) return;
            if (_pos.y + 1 >= _depth) return;
            var target = _grid[_pos.x, _pos.y + 1];
            if (target.HasBackWall()) return;
            _pos.y += 1;
        }
        else if (dir == Vector2Int.down)
        {
            if (currentCell.HasBackWall()) return;
            if (_pos.y - 1 < 0) return;
            var target = _grid[_pos.x, _pos.y - 1];
            if (target.HasFrontWall()) return;
            _pos.y -= 1;
        }

        Vector3 targetPos = new Vector3(_pos.x, fixedHeight, _pos.y);
        if (_rb != null)
        {
            _rb.position = targetPos;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.position = targetPos;
        }
    }

    // Set facing; if rotateVisual flag true, update target rotation for the visual.
    private void SetFacing(Facing f, bool rotateVisual)
    {
        CurrentFacing = f;
        if (rotateVisual && _visual != null)
        {
            float yaw = 0f;
            switch (f)
            {
                case Facing.North: yaw = 0f; break;
                case Facing.East: yaw = 90f; break;
                case Facing.South: yaw = 180f; break;
                case Facing.West: yaw = -90f; break;
            }
            _targetVisualLocalRot = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    public void MoveUp() => Move(Vector2Int.up);
    public void MoveDown() => Move(Vector2Int.down);
    public void MoveLeft() => Move(Vector2Int.left);
    public void MoveRight() => Move(Vector2Int.right);

    public void Shoot()
    {
        if (manager == null) return;

        // Si hay una rejilla válida, comprobar si hay muro justo en frente según la orientación.
        if (_grid != null && _pos.x >= 0 && _pos.x < _width && _pos.y >= 0 && _pos.y < _depth)
        {
            var currentCell = _grid[_pos.x, _pos.y];
            bool wallInFront = false;
            switch (CurrentFacing)
            {
                case Facing.North:
                    wallInFront = currentCell.HasFrontWall();
                    break;
                case Facing.South:
                    wallInFront = currentCell.HasBackWall();
                    break;
                case Facing.East:
                    wallInFront = currentCell.HasRightWall();
                    break;
                case Facing.West:
                    wallInFront = currentCell.HasLeftWall();
                    break;
            }

            if (wallInFront)
            {
                Debug.Log($"{name}: intento de disparo bloqueado por un muro en frente.");
                return;
            }
        }

        Character target = (this == manager.PlayerA) ? manager.PlayerB : manager.PlayerA;

        if (target != null && target.health != null)
        {
            target.health.TakeDamage(20);
            Debug.Log($"{name} disparó a {target.name}, vida restante: {target.health.currentHealth}");
        }
    }

    public bool IsFacing(Facing f) => CurrentFacing == f;

    private void RecoverFromFall()
    {
        Vector3 targetPos = new Vector3(_pos.x, fixedHeight, _pos.y);
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = targetPos;
        }
        else
        {
            transform.position = targetPos;
        }
    }

    private bool HasTagSafe(GameObject go, string tag)
    {
        try { return go != null && go.CompareTag(tag); }
        catch (UnityException) { return false; }
    }   

    private void OnTriggerEnter(Collider other)
    {
        // Detectamos salida por tag "Exit" (si existe) o por componente ExitZone.
        if (HasTagSafe(other.gameObject, "Exit") || other.GetComponent<ExitZone>() != null || other.GetComponentInParent<ExitZone>() != null)
        {
            if (manager != null) manager.PlayerExited(this);
        }
    }
}
