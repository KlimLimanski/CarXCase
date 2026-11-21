using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(10)]
[RequireComponent(typeof(CharacterController))]
public sealed class ApartmentExplorer : MonoBehaviour
{
    public Camera eye;
    public float speed=1.65f;
    public bool externalMovement;
    private CharacterController body;
    private DetectiveGame.Core.OfficerController externalOfficer;
    private float pitch,fall;
    private ApartmentInteractable held;
    private string hint="";
    private int targetFrame = -1;
    private ApartmentInteractable aimedTarget;

    public bool HasPriorityInteraction()
    {
        if (!isActiveAndEnabled || !externalMovement || Cursor.lockState != CursorLockMode.Locked || held)
            return false;
        var target = ResolveAimedTarget();
        return target && target.prioritizeWhenAimed;
    }
    void Start()
    {
        body=GetComponent<CharacterController>();
        externalOfficer=GetComponent<DetectiveGame.Core.OfficerController>();
        if(!externalMovement){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
    }
    void Update()
    {
        // With the hero controller present, doors go through its single, visibility-checked target choice.
        if (externalMovement)
        {
            hint = "";
            return;
        }
        Vector2 move=Vector2.zero,look=Vector2.zero;bool use=false,escape=false,click=false;
#if ENABLE_INPUT_SYSTEM
        var k=Keyboard.current;var m=Mouse.current;
        if(k!=null){move=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));use=k.eKey.wasPressedThisFrame;escape=k.escapeKey.wasPressedThisFrame;}
        if(m!=null){look=m.delta.ReadValue()*.09f;click=m.leftButton.wasPressedThisFrame;}
#else
        move=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
        look=new Vector2(Input.GetAxis("Mouse X"),Input.GetAxis("Mouse Y"))*1.6f;
        use=Input.GetKeyDown(KeyCode.E);escape=Input.GetKeyDown(KeyCode.Escape);click=Input.GetMouseButtonDown(0);
#endif
        if(!externalMovement && escape){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        if(!externalMovement && click){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        if(Cursor.lockState!=CursorLockMode.Locked)return;
        if(!externalMovement){
        transform.Rotate(0,look.x,0);pitch=Mathf.Clamp(pitch-look.y,-80,80);eye.transform.localRotation=Quaternion.Euler(pitch,0,0);
        if(body.isGrounded && fall<0)fall=-1f;fall+=Physics.gravity.y*Time.deltaTime;
        Vector3 direction=transform.TransformDirection(new Vector3(move.x,0,move.y).normalized)*speed;
        direction.y=fall;body.Move(direction*Time.deltaTime);
        }
        ApartmentInteractable target=null;hint="";
        if(held){target=held;hint="E — Положить на место";}
        else
        {
            target = ResolveAimedTarget();
            if (target)
                hint = target.kind == "locked" ? "Заперто" : target.kind == "pickup" ? "E — Взять" : target.IsOpen ? "E — Закрыть" : "E — Открыть";
        }
        if (target)
        {
            // Key prompt in the scene, next to the handle/item (Heavy Rain / Detroit style).
            var point = held ? eye.transform.position + eye.transform.forward * .5f - eye.transform.up * .16f
                             : CarXCase.Interaction.InteractionGlyphs.PointOf(target);
            CarXCase.Interaction.InteractionGlyphs.ShowPrompt(target, point, hint);
        }
        if(use && target && target.kind!="locked"){CarXCase.Interaction.InteractionGlyphs.Press(target);target.Use(eye.transform);if(target.kind=="pickup")held=held?null:target;}
    }
    ApartmentInteractable ResolveAimedTarget()
    {
        if (targetFrame == Time.frameCount) return aimedTarget;
        targetFrame = Time.frameCount;
        aimedTarget = null;
        if (!TryGetInteractionHit(out var hit)) return null;
        if (externalMovement && Vector3.Distance(transform.position + Vector3.up, hit.point) >= 1.8f) return null;
        var candidate = hit.collider.GetComponentInParent<ApartmentInteractable>();
        if (candidate && candidate.isActiveAndEnabled && candidate.playerUsable) aimedTarget = candidate;
        return aimedTarget;
    }

    bool TryGetInteractionHit(out RaycastHit hit)
    {
        hit=default;
        if(!eye)return false;
        var hits=Physics.RaycastAll(eye.transform.position,eye.transform.forward,externalMovement?3.4f:1.65f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
        float nearest=float.PositiveInfinity;bool found=false;
        foreach(var candidate in hits)
        {
            if(candidate.collider.transform.IsChildOf(transform) || candidate.distance>=nearest)continue;
            if (externalMovement && candidate.collider.isTrigger)
            {
                var interactive = candidate.collider.GetComponentInParent<ApartmentInteractable>();
                if (!interactive || !interactive.isActiveAndEnabled) continue;
            }
            hit=candidate;nearest=candidate.distance;found=true;
        }
        return found;
    }
    void OnGUI()
    {
        if(!externalMovement){GUI.Label(new Rect(Screen.width/2f-4,Screen.height/2f-12,20,25),"·",GameTypography.Label);
        GUI.Label(new Rect(20,Screen.height-62,700,30),"WASD — walk   |   Mouse — look   |   E — interact   |   Esc — release cursor",GameTypography.Label);}
        if(!externalMovement)GUI.Label(new Rect(20,Screen.height-37,500,30),hint,GameTypography.Label);
    }
}
