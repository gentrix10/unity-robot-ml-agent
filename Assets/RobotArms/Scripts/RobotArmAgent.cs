using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class RobotArmAgent : Agent
{
    
    [Header("연결")]
    public RobotArmController arm;
    public Transform targetObject;
    
    [Header("보상")]
    public float successReward = 1.0f;
    
    Rigidbody _objectBody;
    float _lastDistance;
    int _successCount;

    public override void Initialize()
    {
        _objectBody = targetObject.GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        arm.ResetPose();
        PlaceObject();

        _lastDistance = Distance();
    }
    
    float Distance() => Vector3.Distance(arm.TipPosition, targetObject.position);
    
    // 물체를 테이블 위 부채꼴 구역의 랜덤한 자리에 놓는다.
    void PlaceObject()
    {
        
        
        Vector3 spot = arm.SpawnPointLocal(
            Random.Range(arm.spawnDistanceRange.x, arm.spawnDistanceRange.y),
            Random.Range(arm.spawnYawRange.x, arm.spawnYawRange.y)
        );
        
        targetObject.SetPositionAndRotation(
            arm.transform.TransformPoint(spot), Quaternion.identity);
        
        // 지난 판에 굴러가던 속도를 지운다.
        _objectBody.linearVelocity = Vector3.zero;
        _objectBody.angularVelocity = Vector3.zero;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        for (int i = 0; i < arm.joints.Length; i++)
        {
            arm.Drive(i, actionBuffers.ContinuousActions[i], Time.fixedDeltaTime);
        }

        GiveReward();
    }

    void GiveReward()
    {
        float distance = Distance();
        
        // object와 TIP의 거리가 가까워질수록 보상을 많이 줄것
        AddReward((_lastDistance - distance) / arm.reach);
        
        // 시간 페널티 한 판 내내 더하면 대략 -1 이 된다.
        AddReward(-1f / MaxStep);
        
        
        _lastDistance = distance;
        const float SuccessDistance = 0.25f;
        if (distance < SuccessDistance)
        {
            AddReward(successReward);
            _successCount++;
            EndEpisode();
        }
    }
    

    public override void CollectObservations(VectorSensor sensor)
    {
        // 관절의 각도 값을 센서 신호로 전송
        for (int i = 0; i < arm.joints.Length; i++)
        {
            sensor.AddObservation(arm.NormalizedAngle(i)); // 3 관절 각도
        }
        // 관절의 속도를 센서 신호로 전송
        for (int i = 0; i < arm.joints.Length; i++)
        {
            sensor.AddObservation(arm.NormalizedVelocity(i)); // 3 관절 각속도
        }
        
        sensor.AddObservation(arm.ToNormalizedLocal(arm.TipPosition)); // 3 팔 끝
        sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position)); // 3 물체
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        for (int i = 0; i < arm.joints.Length && i < continuous.Length; i++)
            continuous[i] = RobotArmInput.Joint(i);
    }
}
