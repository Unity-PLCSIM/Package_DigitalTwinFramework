//----------------------------------------------------------------------------------------------------------------------
// HMI MANAGER
//
// Desc: Gestor global para la visualización del panel HMI principal en la aplicación.
//       Permite alternar la visibilidad del panel mediante atajos de teclado o métodos públicos.
//
// Autor: Alex Asensio
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using UnityEngine;

public class HMIManager : MonoBehaviour
{
    // -- Referencias y Configuración --------------------------------------------

    public GameObject panelHMI;
    public KeyCode teclaToggle = KeyCode.H;

    // -- Ciclo de Vida Unity ----------------------------------------------------

    /// <summary>
    /// Comprueba la entrada del usuario en cada frame para alternar la visibilidad del panel.
    /// </summary>
    void Update()
    {
        if (Input.GetKeyDown(teclaToggle))
            panelHMI.SetActive(!panelHMI.activeSelf);
    }

    // -- API Pública ------------------------------------------------------------

    /// <summary>
    /// Oculta el panel HMI..
    /// </summary>
    public void Cerrar() => panelHMI.SetActive(false);
}