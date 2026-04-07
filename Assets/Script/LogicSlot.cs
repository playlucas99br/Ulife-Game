using UnityEngine;

public class LogicSlot : MonoBehaviour
{
    public LogicType slotType;
    public GameObject caixaParaDestruir;
}

public enum LogicType
{
    AND,
    OR,
    XOR
}