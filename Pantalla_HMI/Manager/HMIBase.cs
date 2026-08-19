//----------------------------------------------------------------------------------------------------------------------
// HMI BASE
//
// Desc: Clase base abstracta para elementos de interfaz HMI.
//       Proporciona la funcionalidad común para escribir valores en los tags del PLC
//       y realizar confirmaciones de lectura automáticas.
//
// Autor: Alex Asensio
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using System;

public abstract class HMIBase : MonoBehaviour
{
    // -- Configuración ----------------------------------------------------------

    public string tagEscritura = "";
    public string tagType      = "Bool";

    // -- Métodos Protegidos -----------------------------------------------------

    /// <summary>
    /// Escribe un valor en el tag configurado del PLC y confirma la escritura realizando una lectura posterior.
    /// </summary>
    /// <param name="valor">Valor a escribir en formato string (ej. "true", "false", "10").</param>
    /// <param name="onResultado">Callback opcional que devuelve true si la confirmación es exitosa.</param>
    /// <param name="onError">Callback opcional invocado si falla la escritura.</param>
    protected void Comunicar(string valor, Action<bool> onResultado = null, Action<string> onError = null)
    {
        ApiInterface.Instance.SetTag(tagEscritura, tagType, valor,
            (_) =>
            {
                // Confirmación leyendo el mismo tag que acabamos de escribir para mantener consistencia[cite: 6]
                ApiInterface.Instance.GetTag(tagEscritura, tagType, (str) =>
                {
                    onResultado?.Invoke(str.Equals("True", StringComparison.OrdinalIgnoreCase));
                },
                (err) => Debug.LogError("[HMIBase] Error GetTag: " + err));
            },
            (err) =>
            {
                Debug.LogError("[HMIBase] Error SetTag: " + err);
                onError?.Invoke(err);
            }
        );
    }
}