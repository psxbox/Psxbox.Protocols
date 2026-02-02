namespace Psxbox.RocProtocol
{
    public enum RocPointType
    {
        DiscreteInputs = 1,
        DiscreteOutputs = 2,
        AnalogInputs = 3,
        AnalogOutputs = 4,
        PulseInputs = 5,
        AGAFlowParameters = 7,
        HistoryParameters = 8,
        AGAFlowValues = 10,
        ROCClock = 12,
        SystemFlags = 13,
        SystemVariables = 15, // System Variables (ROC Information)
        SoftPoints = 17,
        DatabaseSetup = 19,
        GostFlowCalc = 21, // FB103 User defined
        GostMassCalc = 22, // FB103 User difined
        GostProperties = 30, // FB103 User defined
        MVSParameters = 40, // Multi-Variable Sensor (MVS) Parameters
        AGARunParameters = 41,
        ExtraRunParameters = 42,
        MeterFlowValues = 47,
    }
}