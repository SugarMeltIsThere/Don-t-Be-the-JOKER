using UnityEngine;

public class LobbyCameraOrbit : MonoBehaviour
{
    [SerializeField] private Vector3 _target = new Vector3(0f, 0.8f, 0.05f);
    [SerializeField] private float _radius = 1.7f;
    [SerializeField] private float _height = 1.45f;
    [SerializeField] private float _degreesPerSecond = 4f;
    [SerializeField] private float _startAngle = 200f;
    [SerializeField] private float _bobAmount = 0.04f;

    private float _angle;

    // 처음 각도에서 시작한다
    private void Awake()
    {
        _angle = _startAngle;
    }

    // 테이블 둘레를 천천히 돌며 높이를 살짝 오르내리고 테이블 가운데를 바라본다
    private void Update()
    {
        _angle += _degreesPerSecond * Time.deltaTime;
        float rad = _angle * Mathf.Deg2Rad;
        float bob = Mathf.Sin(Time.time * 0.3f) * _bobAmount;
        Vector3 position = new Vector3(_target.x + Mathf.Sin(rad) * _radius, _height + bob, _target.z + Mathf.Cos(rad) * _radius);
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(_target - position, Vector3.up));
    }
}
