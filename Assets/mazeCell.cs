using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
GameObject: MazeCell (attach to each cell prefab)
Descripción: Representa una celda del laberinto y gestiona paredes y estado de visitado.
*/

public class MazeCell : MonoBehaviour
{
    [SerializeField]
    private GameObject _leftWall;

    [SerializeField]
    private GameObject _rightWall;

    [SerializeField]
    private GameObject _frontWall;

    [SerializeField]
    private GameObject _backWall;

    [SerializeField]
    private GameObject _unvisitedBlock;

    public bool IsVisited { get; private set; }

    // Marca la celda como visitada y oculta el bloque de no visitada.
    public void Visit()
    {
        IsVisited = true;
        if (_unvisitedBlock != null) _unvisitedBlock.SetActive(false);
    }

    // Elimina la pared izquierda (desactiva GameObject).
    public void ClearLeftWall()
    {
        if (_leftWall != null) _leftWall.SetActive(false);
    }

    // Elimina la pared derecha (desactiva GameObject).
    public void ClearRightWall()
    {
        if (_rightWall != null) _rightWall.SetActive(false);
    }

    // Elimina la pared frontal (desactiva GameObject).
    public void ClearFrontWall()
    {
        if (_frontWall != null) _frontWall.SetActive(false);
    }

    // Elimina la pared trasera (desactiva GameObject).
    public void ClearBackWall()
    {
        if (_backWall != null) _backWall.SetActive(false);
    }

    // Indica si existe pared izquierda activa.
    public bool HasLeftWall()
    {
        return _leftWall != null && _leftWall.activeSelf;
    }

    // Indica si existe pared derecha activa.
    public bool HasRightWall()
    {
        return _rightWall != null && _rightWall.activeSelf;
    }

    // Indica si existe pared frontal activa.
    public bool HasFrontWall()
    {
        return _frontWall != null && _frontWall.activeSelf;
    }

    // Indica si existe pared trasera activa.
    public bool HasBackWall()
    {
        return _backWall != null && _backWall.activeSelf;
    }
}