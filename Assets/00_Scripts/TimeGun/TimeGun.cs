using UnityEngine;

public class TimeGun : MonoBehaviour
{
    //총알이 나가는 지점
    public Transform firePoint;
    //Raycast의 최대 거리 설정
    public float maxDistance = 100f;
    //디버깅을 위한 시각화 시간
    public float rayDrawDuration = 1f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            FireRaycast();
        }
    }

    void FireRaycast()
    {
        RaycastHit hit;

        //Raycast를 발사할 위치와 방향 설정 (여기서는 firePoint의 정면)
        Vector3 rayOrigin = firePoint.position;
        Vector3 rayDirection = firePoint.forward;

        Debug.DrawRay(rayOrigin, rayDirection * maxDistance, Color.red, rayDrawDuration);

        //Raycast 발사
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, maxDistance))
        {
            //Raycast에 맞은 객체 로그 출력
            Debug.Log("Hit object: " + hit.collider.gameObject.name);

            //맞은 객체에서 Rigidbody 컴포넌트 가져오기
            Rigidbody hitRb = hit.collider.GetComponent<Rigidbody>();

            //Rigidbody가 존재하는지 확인
            if (hitRb != null)
            {
                //현재 isKinematic 상태인지 확인
                if (hitRb.isKinematic == true)
                {
                    //물리 활성화
                    hitRb.isKinematic = false;
                    //중력 활성화
                    hitRb.useGravity = true;
                    Debug.Log(hit.collider.gameObject.name + "의 물리 활성화 (isKinematic: false)");
                }
            }
        }
    }
}
