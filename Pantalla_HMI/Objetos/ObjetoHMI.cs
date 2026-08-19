//----------------------------------------------------------------------------------------------------------------------
// OBJETO HMI
//
// Desc: Clase base abstracta para objetos visuales en la simulación vinculados a tags de PLC.
//       Gestiona la lectura continua (suscripción) de un tag y delega la actualización visual 
//       a las clases derivadas. Permite anidar objetos (ObjetoAsociado).
//
// Autor: Alex Asensio
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using System;

public abstract class ObjetoHMI : HMIBase
{
    // -- Configuración ----------------------------------------------------------

    [Header("Lectura")]
    public string tagLectura = "";
    public ObjetoHMI objetoAsociado;

    // -- Estado Interno ---------------------------------------------------------

    protected bool estadoActual = false;

    // -- Eventos ----------------------------------------------------------------

    /// <summary>Se dispara de forma global cada vez que el estado del tag de lectura cambia.</summary>
    public event Action<bool> OnEstadoCambiado;

    // -- Ciclo de Vida Unity ----------------------------------------------------

    /// <summary>
    /// Inicializa la conexión suscribiéndose a los cambios del tag vía WebSocket 
    /// o enlazándose al evento de un objeto HMI asociado[cite: 8].
    /// </summary>
    protected virtual void Start()
    {
        if (objetoAsociado != null)
        {
            objetoAsociado.OnEstadoCambiado += ActualizarEstado;
        }
        else if (!string.IsNullOrEmpty(tagLectura))
        {
            ApiInterface.Instance.SubscribeOutputTag(tagLectura, (str) =>
                ActualizarEstado(str.Equals("True", StringComparison.OrdinalIgnoreCase)));

            // Lectura forzada para sincronizar el estado inicial
            ApiInterface.Instance.GetTag(tagLectura, tagType, (str) =>
                ActualizarEstado(str.Equals("True", StringComparison.OrdinalIgnoreCase)),
                (err) => Debug.LogError($"[ObjetoHMI] Error lectura inicial '{tagLectura}': " + err));
        }
        else
        {
            Debug.LogWarning($"[ObjetoHMI] '{gameObject.name}' no tiene tagLectura ni objetoAsociado.");
        }
    }

    /// <summary>
    /// Limpia las suscripciones al destruir el objeto para evitar memory leaks.
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (objetoAsociado != null)
            objetoAsociado.OnEstadoCambiado -= ActualizarEstado;
        else if (!string.IsNullOrEmpty(tagLectura))
            ApiInterface.Instance.UnsubscribeOutputTag(tagLectura);
    }

    // -- Lógica Interna ---------------------------------------------------------

    /// <summary>
    /// Procesa el cambio de estado, dispara el evento global e invoca la actualización visual abstracta.
    /// </summary>
    private void ActualizarEstado(bool estado)
    {
        estadoActual = estado;
        OnEstadoCambiado?.Invoke(estado);
        AlActualizar(estado);
    }

    // -- Métodos Abstractos -----------------------------------------------------

    /// <summary>
    /// Método a implementar por las clases derivadas para reaccionar visualmente a los cambios de estado.
    /// </summary>
    protected abstract void AlActualizar(bool estado);
}