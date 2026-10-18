using UnityEngine;
using UnityEngine.InputSystem;

public static class RobotArmInput
{
    // 관절1(어깨). Q = 위로, A = 아래로
    public static float Joint1 => Axis(Key.Q, Key.A);
    // 관절2(팔꿈치). W = 위로, S = 아래로
    public static float Joint2 => Axis(Key.W, Key.S);
    // 관절3(좌우 회전)
    public static float Joint3 => Axis(Key.E, Key.D);
    
    // 잡기
    public static bool Grip => 
        Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

    
    // 자세 초기화. R을 누른 순간 한 번
    public static bool ResetPressed => 
        Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
    
    static float Axis(Key positive, Key negative)
    {
        var kb = Keyboard.current;
        if (kb == null) return 0f;

        return (kb[positive].isPressed ? 1f : 0f) - (kb[negative].isPressed ? 1f : 0f);
    }

    public static float Joint(int i)
    {
        return i switch
        {
            0 => Joint1,
            1 => Joint2,
            2 => Joint3,
            _ => 0f
        };
    }
}
