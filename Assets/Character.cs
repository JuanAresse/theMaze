using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
GameObject: Character (attach to player prefab root)
Descripción: Representa a un personaje en la rejilla del laberinto. Gestiona posición de celda,
movimiento lógico, facing, visual attachment y powerups del personaje.
*/

public class Character : MonoBehaviour
{
    public enum Facing { North, South, East, West }

    private MazeCell[,] _grid;
    private int _width;
    private int _depth;
    private Vector2Int _pos;
    private string _name;

    public TurnBasedManager manager;
    public PlayerHealth health;

    [SerializeField] private float fixedHeight = 0.5f;

    private Rigidbody _rb;
    private Collider _collider;
        
    public Facing CurrentFacing { get; private set; } = Facing.North;

    private Transform _visual;
    [SerializeField] private float visualRotateSpeed = 10f;
    private Quaternion _targetVisualLocalRot = Quaternion.identity;

    [SerializeField] private Vector3 _visualLocalOffset = new Vector3(0f, 0.5f, 0f);

    public Vector2Int CellPosition => _pos;

    public bool queuedPhase = false;
    public bool queuedTrueRadar = false;
    public bool activePhase = false;
    public bool activeTrueRadar = false;

    // Inicializa el Character con la rejilla, posición inicial y nombre.
    // Parámetros: grid - matriz de MazeCell, startPos - posición de inicio, playerName - nombre.
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

    // Actualiza la posición física/visual cada frame tardío.
    private void LateUpdate()
    {
        Vector3 lockedPos = new Vector3(_pos.x, fixedHeight, _pos.y);
        if (_rb != null) _rb.position = lockedPos;
        else transform.position = lockedPos;

        if (_visual != null)
        {
            _visual.localRotation = Quaternion.Slerp(_visual.localRotation, _targetVisualLocalRot, Time.deltaTime * visualRotateSpeed);
        }
    }

    // Ejecuta una lista de acciones (moves) con un delay entre pasos.
    // Parámetros: moves - lista de Action; stepDelay - retardo entre pasos.
    public IEnumerator ExecuteMoves(List<System.Action> moves, float stepDelay = 0.25f)
    {
        if (_grid == null) yield break;

        foreach (var action in moves)
        {
            Vector2Int previous = _pos;
            Facing previousFacing = CurrentFacing;

            Debug.Log($"[ExecuteMoves] {name} - antes: pos={previous}, facing={previousFacing}, próxima acción={action.Method.Name}");

            action.Invoke();

            Debug.Log($"[ExecuteMoves] {name} - después: pos={_pos}, facing={CurrentFacing}");

            if (manager != null && (previous != _pos || previousFacing != CurrentFacing))
            {
                Debug.Log($"[ExecuteMoves] {name} -> reportando cambio anterior pos={previous}, facing={previousFacing} al manager");
                manager.ReportPositionMoved(this, previous, previousFacing);
            }

            yield return new WaitForSeconds(stepDelay);
        }
    }

    // Adjunta una instancia visual al Character, preservando canvases relevantes.
    // Parámetros: visualInstance - GameObject del modelo/visual.
    public void AttachVisual(GameObject visualInstance)
    {
        if (visualInstance == null) return;

        GameObject instance = visualInstance;
        if (visualInstance.scene.rootCount == 0)
        {
            instance = Instantiate(visualInstance);
        }

        Transform visualRoot = transform.Find("VisualRoot");
        if (visualRoot == null)
        {
            var vr = new GameObject("VisualRoot");
            vr.transform.SetParent(transform, false);
            vr.transform.localPosition = _visualLocalOffset;
            vr.transform.localRotation = Quaternion.identity;
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
            visualRoot.localPosition = _visualLocalOffset;
            Vector3 parentScale = transform.localScale;
            visualRoot.localScale = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f
            );
        }

        instance.transform.SetParent(visualRoot, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        _visual = instance.transform;
        _targetVisualLocalRot = _visual.localRotation;

        var allRenderers = GetComponentsInChildren<Renderer>(true);
        var visualRenderers = _visual.GetComponentsInChildren<Renderer>(true);
        var visualRendererSet = new HashSet<Renderer>(visualRenderers);

        foreach (var r in allRenderers)
        {
            if (visualRendererSet.Contains(r)) continue;
            r.enabled = false;
        }

        var healthCanvases = new HashSet<UnityEngine.Canvas>();
        var healthComps = GetComponentsInChildren<PlayerHealth>(true);
        foreach (var hp in healthComps)
        {
            var cs = hp.GetComponentsInChildren<UnityEngine.Canvas>(true);
            foreach (var c in cs) healthCanvases.Add(c);
        }

        var allCanvases = GetComponentsInChildren<UnityEngine.Canvas>(true);
        var visualCanvases = _visual.GetComponentsInChildren<UnityEngine.Canvas>(true);
        var vcSet = new HashSet<UnityEngine.Canvas>(visualCanvases);

        foreach (var c in allCanvases)
        {
            if (vcSet.Contains(c)) continue;
            if (healthCanvases.Contains(c)) continue;
            c.enabled = false;
        }
    }

    // Teletransporta el personaje a una celda (sin comprobaciones adicionales).
    // Parámetros: cellPos - posición de celda destino.
    public void TeleportTo(Vector2Int cellPos)
    {
        _pos = cellPos;
        Vector3 world = new Vector3(_pos.x, fixedHeight, _pos.y);
        if (_rb != null)
        {
            _rb.position = world;
        }
        else
        {
            transform.position = world;
        }

        CheckForExitAtCurrentCell();
    }

    // Mueve al personaje en la dirección indicada y actualiza facing.
    // Parámetros: dir - Vector2Int de dirección (up/down/left/right).
    public void Move(Vector2Int dir)
    {
        if (_grid == null) return;

        if (dir == Vector2Int.up) SetFacing(Facing.North, true);
        else if (dir == Vector2Int.down) SetFacing(Facing.South, true);
        else if (dir == Vector2Int.right) SetFacing(Facing.East, true);
        else if (dir == Vector2Int.left) SetFacing(Facing.West, true);

        var currentCell = _grid[_pos.x, _pos.y];

        if (activePhase)
        {
            if (dir == Vector2Int.right)
            {
                if (currentCell.HasRightWall())
                {
                    if (_pos.x + 1 < _width)
                    {
                        _pos.x += 1;
                        ApplyPositionImmediately();
                        activePhase = false;
                        Debug.Log($"{name} usó Phase: atravesó muro y se colocó en {_pos} (powerup consumido)");
                        return;
                    }
                    else return;
                }
            }
            else if (dir == Vector2Int.left)
            {
                if (currentCell.HasLeftWall())
                {
                    if (_pos.x - 1 >= 0)
                    {
                        _pos.x -= 1;
                        ApplyPositionImmediately();
                        activePhase = false;
                        Debug.Log($"{name} usó Phase: atravesó muro y se colocó en {_pos} (powerup consumido)");
                        return;
                    }
                    else return;
                }
            }
            else if (dir == Vector2Int.up)
            {
                if (currentCell.HasFrontWall())
                {
                    if (_pos.y + 1 < _depth)
                    {
                        _pos.y += 1;
                        ApplyPositionImmediately();
                        activePhase = false;
                        Debug.Log($"{name} usó Phase: atravesó muro y se colocó en {_pos} (powerup consumido)");
                        return;
                    }
                    else return;
                }
            }
            else if (dir == Vector2Int.down)
            {
                if (currentCell.HasBackWall())
                {
                    if (_pos.y - 1 >= 0)
                    {
                        _pos.y -= 1;
                        ApplyPositionImmediately();
                        activePhase = false;
                        Debug.Log($"{name} usó Phase: atravesó muro y se colocó en {_pos} (powerup consumido)");
                        return;
                    }
                    else return;
                }
            }
        }

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

        ApplyPositionImmediately();
    }

    // Aplica la posición física inmediatamente y comprueba salida.
    private void ApplyPositionImmediately()
    {
        Vector3 targetPos = new Vector3(_pos.x, fixedHeight, _pos.y);
        if (_rb != null)
        {
            _rb.position = targetPos;
        }
        else
        {
            transform.position = targetPos;
        }

        CheckForExitAtCurrentCell();
    }

    // Comprueba si la celda actual contiene una ExitZone y notifica al manager.
    private void CheckForExitAtCurrentCell()
    {
        if (_grid == null) return;
        if (_pos.x < 0 || _pos.x >= _width || _pos.y < 0 || _pos.y >= _depth) return;

        var cell = _grid[_pos.x, _pos.y];
        if (cell == null) return;

        var exit = cell.GetComponentInChildren<ExitZone>(true);
        if (exit != null)
        {
            Debug.Log($"[CheckForExit] {name} detected ExitZone at {_pos} -> notifying manager {(manager != null ? manager.name : "null")}");
            if (manager != null) manager.PlayerExited(this);
            else
            {
                var fallback = UnityEngine.Object.FindObjectOfType<TurnBasedManager>();
                if (fallback != null) fallback.PlayerExited(this);
                else Debug.LogWarning("[CheckForExit] No TurnBasedManager found to notify exit.");
            }
        }
    }

    // Ajusta el facing y, de ser necesario, solicita rotación del visual.
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

    // Métodos de conveniencia para movimiento.
    public void MoveUp() => Move(Vector2Int.up);
    public void MoveDown() => Move(Vector2Int.down);
    public void MoveLeft() => Move(Vector2Int.left);
    public void MoveRight() => Move(Vector2Int.right);

    // Aviso: Shoot() sin args no está soportado.
    public void Shoot()
    {
        Debug.LogWarning($"{name}: Shoot() sin argumentos ya no está permitido. Usa Shoot(...Radar...)");
    }

    // Dispara a una celda objetivo; aplica daño si acierta.
    // Parámetros: targetCell - celda objetivo donde se disparará.
    public void ShootAt(Vector2Int targetCell)
    {
        if (manager == null) return;

        Character target = (this == manager.PlayerA) ? manager.PlayerB : manager.PlayerA;
        if (target == null) return;

        if (target.CellPosition == targetCell)
        {
            if (target.health != null)
            {
                target.health.TakeDamage(20);
                Debug.Log($"{name} disparó con radar a {target.name} en {targetCell}, vida restante: {target.health.currentHealth}");
            }
        }
        else
        {
            Debug.Log($"{name} disparó a {targetCell} (radar) y falló. Posición real de {target.name}: {target.CellPosition}");
        }
    }

    // Comprueba si el personaje tiene un tag seguro.
    private bool HasTagSafe(GameObject go, string tag)
    {
        try { return go != null && go.CompareTag(tag); }
        catch (UnityException) { return false; }
    }

    // Evento de trigger que notifica salida si corresponde.
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[OnTriggerEnter] {name} collided with '{(other != null ? other.gameObject.name : "null")}' tag='{(other != null && other.gameObject != null ? other.gameObject.tag : "null")}'");

        if (HasTagSafe(other.gameObject, "Exit") || other.GetComponent<ExitZone>() != null || other.GetComponentInParent<ExitZone>() != null)
        {
            Debug.Log($"[OnTriggerEnter] {name} detected ExitZone -> notifying manager {(manager != null ? manager.name : "null")}")
;
            if (manager != null)
            {
                manager.PlayerExited(this);
            }
            else
            {
                var fallback = UnityEngine.Object.FindObjectOfType<TurnBasedManager>();
                if (fallback != null)
                {
                    Debug.Log("[OnTriggerEnter] Fallback TurnBasedManager found - calling PlayerExited.");
                    fallback.PlayerExited(this);
                }
                else
                {
                    Debug.LogWarning("[OnTriggerEnter] No TurnBasedManager found to notify exit.");
                }
            }
        }
    }

    // Marca la recogida de un powerup (queued para activar en el siguiente turno).
    // Parámetros: type - tipo del powerup.
    public void CollectPowerup(Powerup.PowerupType type)
    {
        if (type == Powerup.PowerupType.Phase)
        {
            queuedPhase = true;
            Debug.Log($"{name} recogió Powerup Phase (disponible next turn).");
        }
        else if (type == Powerup.PowerupType.TrueRadar)
        {
            queuedTrueRadar = true;
            Debug.Log($"{name} recogió Powerup TrueRadar (disponible next turn).");
        }
    }

    // Promueve powerups queued a active al inicio del turno.
    public void PromoteQueuedPowerups()
    {
        if (queuedPhase)
        {
            activePhase = true;
            queuedPhase = false;
            Debug.Log($"{name}: Phase activado para este turno.");
        }
        if (queuedTrueRadar)
        {
            activeTrueRadar = true;
            queuedTrueRadar = false;
            Debug.Log($"{name}: TrueRadar activado para este turno.");
        }
    }

    // Limpia powerups activos al final del turno.
    public void ClearActivePowerups()
    {
        if (activePhase || activeTrueRadar)
        {
            activePhase = false;
            activeTrueRadar = false;
            Debug.Log($"{name}: powerups activos limpiados al final del turno.");
        }
    }
}
