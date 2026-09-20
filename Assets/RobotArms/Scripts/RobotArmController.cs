using System.Collections.Generic;
using UnityEngine;

public class RobotArmController : MonoBehaviour
{
    [System.Serializable]
    
    public class Joint
    {
        public string label = "관절";
        public Transform pivot;
        public Vector3 axis = Vector3.left;
        public float minAngle = -90f;
        public float maxAngle = 90f;
        public float maxSpeed = 120f;
        public float startAngle = 0f;
        
        public float Angle { get; private set; }
        public float Velocity { get; private set; }

        public void Drive(float normalizedSpeed, float deltaTime)
        {
            float beforeAngle = Angle;
            float deltaAngle = Mathf.Clamp(normalizedSpeed, -1f, 1f) * maxSpeed * deltaTime;

            Angle = Mathf.Clamp(Angle + deltaAngle, minAngle, maxAngle);
            Velocity = deltaTime > 0f ? (Angle - beforeAngle) / deltaTime : 0f;

            Apply();
        }

        public void ResetTo(float angle)
        {
            Angle = Mathf.Clamp(angle, minAngle, maxAngle);
            Velocity = 0f;
            Apply();
        }

        void Apply()
        {
            if (pivot != null)
            {
                pivot.localRotation = Quaternion.AngleAxis(Angle, axis);
            }
            
        }
    }
    
    public Joint[] joints = new Joint[0];

    void Awake()
    {
        ResetPose();
    }

    public void ResetPose()
    {
        foreach (var j in joints)
        {
            j.ResetTo(j.startAngle);
        }
    }

    public void Drive(int index, float normalizedSpeed, float deltaTime)
    {
        joints[index].Drive(normalizedSpeed, deltaTime);
    }
  
    
    // Update is called once per frame
    void FixedUpdate()
    {
        if(RobotArmInput.ResetPressed) ResetPose();

        for (int i = 0; i < joints.Length; i++)
        {
            Drive(i, RobotArmInput.Joint(i), Time.fixedDeltaTime);
        }
    }

    public Transform tip;

    public float floorHeight = 0f;

    private Transform[] _chain;
    Transform[] Chain
    {
        get
        {
            if (_chain != null && _chain.Length > 0) return _chain;

            var list = new List<Transform>();
            for (var t= tip; t != null && t != transform; t = t.parent)
            {
                list.Add(t);
            }
            list.Reverse();
            return _chain = list.ToArray();
        }
    }

    public bool IsBlocked()
    {
        return false;
    }
}
