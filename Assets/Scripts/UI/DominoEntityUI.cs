using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DominoEntityUI : MonoBehaviour
{
    [SerializeField] private TextMeshPro topText;
    [SerializeField] private TextMeshPro bottomText;

    private DominoEntity dominoEntity;

    public void SetDominoInfo(DominoEntity dominoEntity)
    {
        this.dominoEntity = dominoEntity;

        topText.SetText(dominoEntity.TopScore.ToString());
        bottomText.SetText(dominoEntity.BottomScore.ToString());
    }

    public void OnTopScoreChanged(int oldValue, int newValue)
    {
        if (dominoEntity.Flipped)
        {
            bottomText.SetText(newValue.ToString());
            return;
        }

        topText.SetText(newValue.ToString());
    }

    public void OnBottomScoreChanged(int oldValue, int newValue)
    {
        if (dominoEntity.Flipped)
        {
            topText.SetText(newValue.ToString());
            return;
        }

        bottomText.SetText(newValue.ToString());
    }

    public void OnFlippedChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            topText.SetText(dominoEntity.BottomScore.ToString());
            bottomText.SetText(dominoEntity.TopScore.ToString());
            return;
        }

        topText.SetText(dominoEntity.TopScore.ToString());
        bottomText.SetText(dominoEntity.BottomScore.ToString());
    }
}
