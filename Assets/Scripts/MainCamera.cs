/*
 * Purpose: Handles camera movement
 */

using UnityEngine;
using UnityEngine.InputSystem;
using Cursor = UnityEngine.Cursor;

public class MainCamera : MonoBehaviour
{
    [Header("Placement of Camera")]
    [SerializeField] private Transform camSpot;

    [Header("Input Actions References")]
    [SerializeField] private InputActionReference clickAndDrag;
    [SerializeField] private InputActionReference doubleClick;
    [SerializeField] private InputActionReference mouseDelta; // Tracks change in mouse coordinates
    [SerializeField] private InputActionReference mousePos;

    // Input actions
    private InputAction clickAndDragIA;
    private InputAction doubleClickIA;
    private InputAction mouseDeltaIA;
    private InputAction mousePosIA;

    [Header("Look Sensitivity & Momentum")]
    [SerializeField] private float sensitivity = 1000f;
    [SerializeField] private float momentumDropoff = 0.1f; // The factor by which momentum decreases
    [SerializeField] private float momentumThreshold = 1.0f; // The limit value for momentum to be set to 0

    [Header("Camera Node Script")]
    [SerializeField] private CameraNode camNode;

    // Rotational coordinates
    private float yaw; // Horizontal movement
    private float pitch; // Vertical movement

    // Momentum for lingering camera movement
    private Vector2 momentum;

    // Variables for raycasting
    private Camera mainCam;
    private Collider currentNodeCollider;

    // Enumerator to help prevent overlap between left-click responses
    private enum CameraState
    {
        Idle,
        Dragging,
        Moving
    }

    private CameraState state = CameraState.Idle;

    // New variables 4/25
    private Vector3 startMovePos; // just gonna be camSpot
    private Quaternion startMoveRot; // Gonna be the rotation of the cammera

    private Vector3 endMovePos;
    private Quaternion endMoveRot;

    private float elapsedTime = 0f;
    private float progress = 0f;

    private CameraNode targetNode;

    private CameraNode.CamConnections foundConnection = default;
    bool isConnected = false;

    private void Awake()
    {
        // Assign input action references to input action equivalents for easier access
        clickAndDragIA = clickAndDrag.action;
        doubleClickIA = doubleClick.action;
        mouseDeltaIA = mouseDelta.action;
        mousePosIA = mousePos.action;

        // Initialize main camera for later raycast use
        mainCam = Camera.main;
    }

    private void OnEnable()
    {
        // Enable input actions
        clickAndDrag.action.Enable();
        doubleClick.action.Enable();
        mouseDelta.action.Enable();
        mousePos.action.Enable();

        // Subscribe double click action to related function
        doubleClickIA.performed += DoubleClicked;
    }

    private void Start()
    {
        if (camSpot == null)
        {
            Debug.LogError("Camera spot is not assigned.");
            return;
        }
        if (isConnected && foundConnection.moveDuration <= 0f)
        {
            Debug.LogError("Invalid Move Duration value. Must be greater than 0.");
        }

        // Get the collider for the camera node that is initially visited and disable it to prevent raycast interference
        currentNodeCollider = camNode.GetComponent<Collider>();
        if (currentNodeCollider != null)
        {
            currentNodeCollider.enabled = false;
        }

        // Set initial camera rotation and momentum
        yaw = camSpot.rotation.eulerAngles.y;
        pitch = camSpot.rotation.eulerAngles.x;
        momentum = Vector2.zero;
        state = CameraState.Idle;
    }

    void Update()
    {
        if (state == CameraState.Moving)
        {
            elapsedTime += Time.deltaTime;

            float t = Mathf.Clamp01(elapsedTime / foundConnection.moveDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            mainCam.transform.position = Vector3.Lerp(startMovePos, endMovePos, smoothT);
            if (elapsedTime >= foundConnection.rotationDelay)
            {
                float rotT = Mathf.Clamp01((elapsedTime - foundConnection.rotationDelay) / (foundConnection.moveDuration - foundConnection.rotationDelay));
                float smoothRotT = Mathf.SmoothStep(0f, 1f, rotT);
                mainCam.transform.rotation = Quaternion.Slerp(startMoveRot, endMoveRot, smoothRotT);
            }
            
            progress = elapsedTime / foundConnection.moveDuration;
            if (progress >= 1f)
            {
                mainCam.transform.position = endMovePos;
                mainCam.transform.rotation = endMoveRot;

                momentum = Vector2.zero;

                yaw = endMoveRot.eulerAngles.y;
                pitch = endMoveRot.eulerAngles.x;

                camNode = targetNode;

                camSpot = camNode.transform;

                elapsedTime = 0;

                Cursor.visible = true;

                foundConnection = default;
                isConnected = false;

                state = CameraState.Idle;
            }
            return;
        }

        HandleInput();

        // Clamp the pitch and yaw so that it can't exceed the limits
        pitch = Mathf.Clamp(pitch, camNode.pitchLimits.x, camNode.pitchLimits.y); // Typically (-90 to 90 degrees)
        if (camNode.hasYawLimits) // If the camera has a limited horizontal range, clamp
        {
            yaw = Mathf.Clamp(yaw, camNode.yawLimits.x, camNode.yawLimits.y);
        }

        // If the camera is being dragged, change the camera rotation via pitch and yaw values
        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0);

        RotateCamera(camRotation);
    }

    /*
     * Is called every frame.
     * If the screen is being dragged, or momentum is substantially large, the camera rotates accordingly.
     */
    public void HandleInput()
    {
        // Initialize inputDelta to 0
        Vector2 inputDelta = Vector2.zero; // (0, 0)

        // Checks if the mouse is pressed, and updates inputDelta and increases momentum if so.
        if (clickAndDragIA.IsPressed() && mouseDeltaIA.ReadValue<Vector2>().magnitude > 5 || clickAndDragIA.IsPressed() && state == CameraState.Dragging)
        {
            if (state != CameraState.Dragging)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = true;
            }
            state = CameraState.Dragging;
            inputDelta = mouseDeltaIA.ReadValue<Vector2>();
            momentum = Vector2.Lerp(momentum, inputDelta, 0.1f); // Linearly increase momentum in steps of 0.1
        }
        // Checks if momentum is substantially large, and updates inputDelta and decerases momentum if so.
        else if (momentum.sqrMagnitude > momentumThreshold)
        {
            if (state == CameraState.Dragging)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            state = CameraState.Idle;
            inputDelta = momentum;
            momentum = Vector2.Lerp(momentum, Vector2.zero, momentumDropoff * Time.deltaTime);
        }
        // If none of these conditions are satisfied, set momentum to 0.
        else
        {
            if (state == CameraState.Dragging)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            state = CameraState.Idle;
            momentum = Vector2.zero;
        }

        // Update yaw and pitch according to inputDelta and sensitivity
        yaw += inputDelta.x * sensitivity * Time.deltaTime;
        pitch -= inputDelta.y * sensitivity * Time.deltaTime;
    }

    /* 
     * Is called when double click is performed.
     * If the mouse double clicked on a camera node's collider, move to it.
     */
    public void DoubleClicked(InputAction.CallbackContext context)
    {
        // Shoots out a ray from the camera through the mouse point
        LayerMask mask = LayerMask.GetMask("CameraNode");
        Ray ray = mainCam.ScreenPointToRay(mousePosIA.ReadValue<Vector2>());
        RaycastHit hit;

        Debug.DrawLine(ray.origin, ray.origin + ray.direction * 100f, Color.red, 1f);

        // If a collider in the "CameraNode" layer is hit, re-enable the current camera's collider, and move the camera to the saved hit collider
        if (!Physics.Raycast(ray, out hit, 1000f, mask)) return;
        
        Collider hitCollider = hit.collider;
        Debug.Log("Hit: " + hitCollider.name);

        // Get the collider's CameraNode script.
        targetNode = hitCollider.GetComponent<CameraNode>();

        if (targetNode == null) // If it doesn't exist, send an error log as that means a camera node is missing this script.
        {
            Debug.LogError("No Camera Node found");
            return;
        }

        foundConnection = default;
        isConnected = false;

        foreach (var connection in camNode.connections)
        {
            if (connection.targetNode == targetNode)
            {
                foundConnection = connection;
                isConnected = true;
                break;
            }
        }

        if (!isConnected)
        {
            Debug.Log("Target node is not connected to the current node");
            state = CameraState.Idle;
            return;
        }

        if (currentNodeCollider != null)
        {
            currentNodeCollider.enabled = true;
        }

        MoveCameraToHitNode(hitCollider);
    }

    /*
     * Is called when a camera node's collider is double clicked on.
     * Parameter is hit camera node's collider.
     * Move camera's position and rotation to camera Node, and save its collider.
     */
    void MoveCameraToHitNode(Collider hitColl)
    {
        state = CameraState.Moving;

        momentum = Vector2.zero;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;

        startMovePos = camSpot.position;
        startMoveRot = mainCam.transform.rotation;

        endMovePos = targetNode.getPosition();
        endMoveRot = targetNode.transform.rotation;

        // Re-set the current node collider to what was hit and disable it to prevent raycast interference
        currentNodeCollider = hitColl;
        if (currentNodeCollider != null)
        {
            currentNodeCollider.enabled = false;
        }
    }

    /*
     * Is called every frame.
     * Parameter is rotation, which is determined by pitch and yaw variables, which change with mouse drag.
     */
    void RotateCamera(Quaternion rotation)
    {
        transform.position = camSpot.position;
        transform.rotation = rotation;
    }

    void OnDisable()
    {
        doubleClickIA.performed -= DoubleClicked;

        clickAndDragIA.Disable();
        doubleClickIA.Disable();
        mouseDeltaIA.Disable();
        mousePosIA.Disable();
    }
}
