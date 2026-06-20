using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shows validation hints when the user clicks Continuar while the button is disabled.
/// </summary>
public class ConsentContinueDisabledClick : MonoBehaviour, IPointerClickHandler
{
    ConsentUIController controller;
    Button continueButton;

    public void Bind(ConsentUIController owner, Button button)
    {
        controller = owner;
        continueButton = button;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (continueButton == null || continueButton.interactable || controller == null)
            return;

        controller.RevealValidationHints();
    }
}
