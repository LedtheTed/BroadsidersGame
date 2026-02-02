using UnityEngine;
using UnityEngine.InputSystem;

public class RTSCameraController : MonoBehaviour
{
    
    [Header("References")]
    [SerializeField] private Transform pivot;           // RTSCameraPivot
    [SerializeField] private Camera cam;                // main camera

    [Header("Pan")]
    [SerializeField] private float panSpeed = 30f;
    [SerializeField] private float panBoostMultiplier = 2f;
    [SerializeField] private bool edgePanEnabled = false;
    [SerializeField] private float edgePanBorderPx = 15f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 30f;
    [SerializeField] private float minZoom = 10f;
    [SerializeField] private float maxZoom = 80f;
    [SerializeField] private float zoomSmoothing = 12f;


    [Header("Rotation")]
    [SerializeField] private bool rotationEnabled = true;
    [SerializeField] private float rotateSpeedDegPerSec = 120f;

    [Header("Pitch")]
    [SerializeField] private float pitchDeg = 45f;

    [Header("Ground Plane")]
    [SerializeField] private float groundY = 0f;            // set to water level

    [Header("Bounds")]
    [SerializeField] private bool clampToBounds = false;
    [SerializeField] private Vector2 xBounds = new Vector2(-200f, 200f);
    [SerializeField] private Vector2 zBounds = new Vector2(-200f, 200f);


    private float targetZoom;
    private bool dragging = false;
    private Vector3 dragOriginWorld;

    private void Awake(){
        if(cam == null) cam = Camera.main;
        if(pivot == null && transform.childCount > 0) pivot = transform.GetChild(0);

        if(cam == null || pivot == null){
            Debug.LogError("[RTSCameraController] missing cam or pivot reference");
        }  

        targetZoom = Mathf.Abs(cam.transform.localPosition.z);  // init zoom
    }


    private void LateUpdate(){
        if(cam == null || pivot == null) return;
        if(Mouse.current == null || Keyboard.current == null) return;

        float dt = Time.deltaTime;

        Vector3 pe = pivot.localEulerAngles;
        pe.x = pitchDeg;
        pivot.localEulerAngles = pe;

        HandlePan(dt);
        HandleZoom(dt);
        HandleRotate(dt);

        if(clampToBounds){
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, xBounds.x, xBounds.y);
            p.z = Mathf.Clamp(p.z, zBounds.x, zBounds.y);
            transform.position = p;
        }

    }


    // Middle Mouse Drag
    private void HandlePan(float dt){
        if(Mouse.current.middleButton.wasPressedThisFrame){
            dragging = true;
            dragOriginWorld = ScreenToGround(Mouse.current.position.ReadValue());
        }
        if(Mouse.current.middleButton.wasReleasedThisFrame){
            dragging = false;
        }
        if(dragging && Mouse.current.middleButton.isPressed){
            Vector3 currentWorld = ScreenToGround(Mouse.current.position.ReadValue());
            Vector3 delta = dragOriginWorld - currentWorld;
            delta.y = 0f;
            transform.position += delta;
            return; // overrides wasd moving
        }

        Vector3 move = Vector3.zero;

        // WASD / arrow keys
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move += Vector3.forward;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move += Vector3.back;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move += Vector3.left;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move += Vector3.right;

        // edge pan
        if(edgePanEnabled){
            Vector2 mp = Mouse.current.position.ReadValue();
            float w = Screen.width;
            float h = Screen.height;

            if(mp.x <= edgePanBorderPx){ 
                move += Vector3.left;
            } else if(mp.x >= w-edgePanBorderPx){
                move += Vector3.right;
            } 

            if(mp.y <= edgePanBorderPx){ 
                move += Vector3.back;
            } else if(mp.y >= h-edgePanBorderPx){
                move += Vector3.forward;
            } 
        }

        if(move.sqrMagnitude < 0.0001f) return;
        move = move.normalized;

        float speed = panSpeed;

        if(Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed){
            speed *= panBoostMultiplier;
        }

        Vector3 worldMove = transform.TransformDirection(move);
        worldMove.y = 0f;

        transform.position += worldMove * speed * dt;

    }


    private void HandleZoom(float dt){

        Vector2 scrollDelta = Mouse.current.scroll.ReadValue();
        float scroll = scrollDelta.y;

        if(Mathf.Abs(scroll) > 0.01f){
            // scroll up is zoom in
            targetZoom -= scroll * 0.01f * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        Vector3 localPos = cam.transform.localPosition;
        float currentZoom = Mathf.Abs(localPos.z);

        float newZoom = Mathf.Lerp(currentZoom, targetZoom, 1f - Mathf.Exp(-zoomSmoothing * dt));
        localPos.z = -newZoom;

        cam.transform.localPosition = localPos;

    }


    private void HandleRotate(float dt){

        if(!rotationEnabled) return;

        float yawInput = 0f;
        if (Keyboard.current.qKey.isPressed) yawInput -= 1f;
        if (Keyboard.current.eKey.isPressed) yawInput += 1f;

        if(Mathf.Abs(yawInput) < 0.01f) return;

        transform.Rotate(Vector3.up, yawInput * rotateSpeedDegPerSec * dt, Space.World);

    }


    private Vector3 ScreenToGround(Vector2 screenPos){
        Ray ray = cam.ScreenPointToRay(screenPos);

        Plane plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        if(plane.Raycast(ray, out float enter)){
            return ray.GetPoint(enter);
        }
        return transform.position;
    }





}
