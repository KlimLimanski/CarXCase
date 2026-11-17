using UnityEngine;

public sealed class ApartmentInteractable : MonoBehaviour
{
    public string kind = "hinge";
    public Vector3 localAxis = Vector3.up;
    public float openAngle = 95f;
    public float travel = .38f;
    public bool prioritizeWhenAimed;
    private Quaternion closedRotation;
    private Vector3 closedPosition;
    private bool open;
    private bool held;
    private Transform originalParent;
    private Vector3 originalScale;
    private Collider[] itemColliders;
    private bool[] colliderStates;
    void Awake()
    {
        closedRotation = transform.localRotation;
        closedPosition = transform.localPosition;
    }
    public void EnableOpenedHinge(Quaternion closed, Vector3 axis, float angle)
    {
        closedRotation = closed;
        localAxis = axis;
        openAngle = angle;
        open = true;
        enabled = true;
    }

    public void Use(Transform eye)
    {
        if(kind=="locked")return;
        if (kind != "pickup") { open = !open; return; }
        if (!held)
        {
            originalParent = transform.parent;
            originalScale = transform.localScale;
            closedPosition = transform.localPosition;
            closedRotation = transform.localRotation;
            itemColliders = GetComponentsInChildren<Collider>();
            colliderStates = new bool[itemColliders.Length];
            for (int i=0; i<itemColliders.Length; i++)
            { colliderStates[i]=itemColliders[i].enabled; itemColliders[i].enabled=false; }
            transform.SetParent(eye, true);
            // Center the visible geometry, even if the mesh origin is not at its center.
            var renderers=GetComponentsInChildren<Renderer>();
            Bounds b=new Bounds(transform.position,Vector3.zero);
            if(renderers.Length>0){b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);}
            transform.position += eye.position + eye.forward*.5f - b.center;
        }
        else
        {
            transform.SetParent(originalParent, false);
            transform.localPosition=closedPosition; transform.localRotation=closedRotation;
            transform.localScale=originalScale;
            for(int i=0;i<itemColliders.Length;i++) if(itemColliders[i]) itemColliders[i].enabled=colliderStates[i];
        }
        held=!held;
    }
    void Update()
    {
        if(kind=="hinge") transform.localRotation=Quaternion.Slerp(transform.localRotation,
            closedRotation*Quaternion.AngleAxis(open ? openAngle:0,localAxis),Time.deltaTime*5f);
        else if(kind=="drawer") transform.localPosition=Vector3.Lerp(transform.localPosition,
            closedPosition+(open ? localAxis*travel:Vector3.zero),Time.deltaTime*5f);
    }
}
