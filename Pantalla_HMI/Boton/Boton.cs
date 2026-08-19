//----------------------------------------------------------------------------------------------------------------------
// BOTON HMI
//
// Desc: Componente de interfaz que actúa como un botón para interactuar con la API del PLC.
//       Al ser pulsado, lee el estado actual de un tag y escribe su valor opuesto (toggle).
//
// Uso:  Asignar a un GameObject con un componente de UI (ej. Button) y vincular el método AlPulsar()
//       al evento OnClick de dicho botón.
//
// Autor: Alex Asensio
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using System;

public class Boton : HMIBase
{
    // -- API Pública ------------------------------------------------------------

    /// <summary>
    /// Lee el estado actual del tag configurado y envía el estado inverso al PLC.
    /// </summary>
    public void AlPulsar()
    {
        if (string.IsNullOrEmpty(tagEscritura)) 
        { 
            Debug.LogWarning("[Boton] tagEscritura no asignado."); 
            return; 
        }

        Debug.Log("[Boton] Pulsado");
        
        // Se requiere lectura del estado actual antes de conmutar (toggle)[cite: 5]
        ApiInterface.Instance.GetTag(tagEscritura, tagType, (str) =>
        {
            bool estadoActual = str.Equals("True", StringComparison.OrdinalIgnoreCase);
            string nuevoValor = estadoActual ? "false" : "true";
            
            ApiInterface.Instance.SetTag(tagEscritura, tagType, nuevoValor,
                (msg) => Debug.Log("[Boton] OK: " + msg),
                (err) => Debug.LogError("[Boton] Error: " + err)
            );
        },
        (err) => Debug.LogError("[Boton] Error al leer estado actual: " + err));
    }
}