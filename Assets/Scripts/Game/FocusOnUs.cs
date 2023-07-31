using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Helpers;
using UnityEngine;

public class FocusOnUs : MonoBehaviour
{
    private Camera mainCamera;
    [SerializeField] private int lineCount = 4;

    private Vector3 lineCenterPosition;

    private void Start()
    {
        mainCamera = Camera.main;
        lineCenterPosition = Vector3.zero;
    }

    // this created a line but I could not zoom into it
    public void CreateLine(bool horizontal = true)
    {
        List<GameObject> lineObjects = new List<GameObject>();
        
        for (int i = 0; i < lineCount; i++)
        {
            var newObj = Instantiate(gameObject);
            if (horizontal)
            {
                // top to bottom
                newObj.transform.localRotation = Quaternion.Euler(new Vector3(0, 90, 90));
;               // bottom to top (flipped domino)
                //newObj.transform.Rotate(new Vector3(0, 90, 90));
            }

            lineObjects.Add(newObj);
        }
        
        PositionHelper.LayoutInLine(gameObject.transform.position, lineObjects, mainCamera);
        gameObject.SetActive(false);
    }
    

}
