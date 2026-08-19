//----------------------------------------------------------------------------------------------------------------------
// VARIADOR MAIN SCRIPT
//
// Desc: Simula el comportamiento físico y lógico de un variador de frecuencia.
//       Lee consignas y comandos del PLC, calcula la cinemática en tiempo real (rampas)
//       y transmite el feedback (encoder virtual y estado) de vuelta al PLC.
//
// Uso:  Asignar al GameObject correspondiente a la mesa/motor. Configurar tags en el inspector.
//
// Autor: Alex Asensio
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using System;
using System.Globalization;

/// <summary>
/// Plantilla Genérica de Variador.
/// </summary>
public class VariadorMainScript : MonoBehaviour
{
    // -- Comandos PLC -> Unity --------------------------------------------------
    // Valores por defecto configurados para MESA 1 del programa Nuevo_Choco
    [Header("--- COMANDOS (Outputs del PLC -> Unity) ---")]
    public string tagEnable = "Mesa1_CW1_Bit0";
    public string tagStart = "Mesa1_CW1_Bit7";
    public string tagConsignaVelocidad = "Mesa1_CW2_SpeedSetpoint";

    // -- Feedback Unity -> PLC --------------------------------------------------

    [Header("--- FEEDBACK (Unity -> Inputs del PLC) ---")]
    public string tagFeedbackStatusWord = "Mesa1_SW1_StatusWord";
    public string tagFeedbackVelocidad = "Mesa1_SW2_SpeedFB";
    
    [Tooltip("Tipo de dato exacto esperado por la API")]
    public string tipoDatoFeedback = "UInt (UInt16)";

    // -- Parámetros Mecánicos ---------------------------------------------------

    [Header("--- PARÁMETROS MECÁNICOS ---")]
    public float aceleracion = 500f;
    public float deceleracion = 500f;
    public float tasaRefrescoEscritura = 0.2f; 

    // -- Estado Interno y Telemetría --------------------------------------------

    [Header("--- ESTADO INTERNO (Telemetría) ---")]
    public bool isEnabled = false;
    public bool isStarted = false;
    public float consignaObjetivo = 0f;
    public float velocidadActualFisica = 0f;

    private float timerEscritura = 0f;

    [Header("--- MESA ASOCIADA ---")]
    public GameObject mesa;

    // -- Ciclo de Vida Unity ----------------------------------------------------

    /// <summary>
    /// Suscribe los tags de entrada asociados para recibir comandos continuamente.
    /// </summary>
    void Start()
    {
        string en = tagEnable.Trim();
        string st = tagStart.Trim();
        string sp = tagConsignaVelocidad.Trim();

        ApiInterface.Instance.SubscribeTag(en, "Bool", (v) => isEnabled = ParseBool(v));
        ApiInterface.Instance.SubscribeTag(st, "Bool", (v) => isStarted = ParseBool(v));
        ApiInterface.Instance.SubscribeTag(sp, "UInt (UInt16)", (v) => consignaObjetivo = ParseFloat(v));
    }

    /// <summary>
    /// Libera las suscripciones en memoria para evitar colisiones en la API.
    /// </summary>
    private void OnDestroy()
    {
        if (ApiInterface.Instance != null)
        {
            ApiInterface.Instance.UnsubscribeTag(tagEnable.Trim());
            ApiInterface.Instance.UnsubscribeTag(tagStart.Trim());
            ApiInterface.Instance.UnsubscribeTag(tagConsignaVelocidad.Trim());
        }
    }

    /// <summary>
    /// Bucle físico donde se recalcula el movimiento y se transmite el estado al PLC.
    /// </summary>
    void FixedUpdate()
    {
        CalcularCinematica();
        TransmitirFeedback();
        MoverMecanismo();
    }

    // -- Lógica Física y Cinemática ---------------------------------------------

    /// <summary>
    /// Ejecuta el movimiento visual/físico de la mesa acoplada.
    /// </summary>
    void MoverMecanismo()
    {  
        // TODO: conexión lógica final con el transform de la mesa
        return;
    }

    /// <summary>
    /// Aplica las rampas de aceleración o frenado según el estado de la habilitación y la consigna.
    /// </summary>
    private void CalcularCinematica()
    {
        float targetSpeed = (isEnabled && isStarted) ? consignaObjetivo : 0f;
        float rampaActiva = (targetSpeed == 0f && isEnabled) ? deceleracion : aceleracion;

        // Si cae la habilitación (Coast to stop), clava los frenos
        if (!isEnabled) rampaActiva = 99999f; 

        velocidadActualFisica = Mathf.MoveTowards(
            velocidadActualFisica, 
            targetSpeed, 
            rampaActiva * Time.fixedDeltaTime
        );
    }

    /// <summary>
    /// Envía datos del encoder virtual y del estado general limitando la frecuencia 
    /// según la tasa de refresco configurada.
    /// </summary>
    private void TransmitirFeedback()
    {
        timerEscritura += Time.fixedDeltaTime;
        
        if (timerEscritura >= tasaRefrescoEscritura)
        {
            timerEscritura = 0f;

            // Transmisión de Estado
            int statusWordValue = isEnabled ? 1 : 0;
            ApiInterface.Instance.SetTagInt(tagFeedbackStatusWord.Trim(), tipoDatoFeedback, statusWordValue);

            // Transmisión de Velocidad (Encoder Virtual)
            int velocidadParaPLC = Mathf.RoundToInt(velocidadActualFisica);
            ApiInterface.Instance.SetTagInt(tagFeedbackVelocidad.Trim(), tipoDatoFeedback, velocidadParaPLC);
        }
    }

    // -- MÉTODOS DE DECODIFICACIÓN ROBUSTA --------------------------------------

    /// <summary>
    /// Decodifica el texto que proviene de la API garantizando seguridad para valores booleanos.
    /// </summary>
    private bool ParseBool(string valor)
    {
        if (string.IsNullOrEmpty(valor)) return false;
        // Valida tanto formatos textuales como binarios
        return valor.Equals("True", StringComparison.OrdinalIgnoreCase) || valor.Trim() == "1";
    }

    /// <summary>
    /// Decodifica valores numéricos aplicando `CultureInfo.InvariantCulture` para evitar fallos regionales.
    /// </summary>
    private float ParseFloat(string valor)
    {
        if (string.IsNullOrEmpty(valor)) return 0f;
        
        if (float.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out float result))
            return result;
        
        return 0f;
    }
}