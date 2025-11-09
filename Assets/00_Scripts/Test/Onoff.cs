using UnityEngine;

public class Onoff : MonoBehaviour
{
    float x;
    float y;
    float z;

    public GameObject gameobject;
    public int spinSpeed;
    public bool isOn = false;

    void Start()
    {
        x = gameObject.transform.rotation.eulerAngles.x;
        y = gameObject.transform.rotation.eulerAngles.y;
        z = gameObject.transform.rotation.eulerAngles.z;
    }

    
    void Update()
    {
        CheckSystem();
    }

    private void CheckSystem()
    {
        if(isOn == true)
        {
            x += spinSpeed * Time.deltaTime;
            gameObject.transform.eulerAngles = new Vector3(x, y, z);
        }
        if(isOn == false)
        {
            x = gameObject.transform.rotation.eulerAngles.x;
            gameObject.transform.eulerAngles = new Vector3(x, y, z);
        }
    }
}
