using UnityEngine;

public class UndoButton : MonoBehaviour
{
    [SerializeField] private ConsumableButton consumableButton;

    private void Awake()
    {
        if (consumableButton != null)
            consumableButton.CanActivate = () => UndoManager.Instance != null && UndoManager.Instance.CanUndo && !MoveCounter.IsOutOfMoves;
    }
}
