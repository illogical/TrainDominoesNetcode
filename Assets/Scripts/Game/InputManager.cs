using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public Camera MainCamera;
    public event EventHandler<int> DominoClicked;


    void Update()
    {
        GetMouseClick();
    }

    void GetMouseClick()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        Ray ray = MainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            var dominoEntity = hit.transform.gameObject.GetComponent<DominoEntityUI>();
            DominoEntity dominoInfo = dominoEntity.DominoInfo;
            if (dominoInfo == null)
            {
                return;
            }

            MouseClickedObject(dominoInfo.ID, dominoInfo.Purpose);
        }

        void MouseClickedObject(int id, PurposeType purpose)
        {
            Debug.Log($"Domino {id} ({purpose}) was clicked via InputManager");

            DominoClicked?.Invoke(this, id);
        }
    }
}
