using UnityEngine;
using System.Collections;
using System;
using Random = UnityEngine.Random;
using UnityEngine.InputSystem;
using Munnica.THL.Managers;
using Munnica.THL.Interactives;
using Munnica.THL.UI;
using Munnica.THL.Inventories;

namespace Munnica.THL.Controllers
{
    [RequireComponent(typeof(AudioSource), typeof(CharacterController))]
    public class FPCC : MonoBehaviour
    {
        public static FPCC Instance;

        //SELECT INPUT SYSTEM
        public enum InputSystem { legacy, newInputSystem }

        public InputSystem inputSystem;
       
        [SerializeField, Tooltip("Turn off/on movement when F1 is pressed when enabled")] private bool debugMode; 

        
        //SELECT DETECTION SYSTEM
        public enum DetectionSystem { none, interfaces, abstractClasses }
        public DetectionSystem detectionSystem;

        public bool CanPause { get {  return canPause; } set { canPause = value; } }

        public bool CanMove { get; set; } = true;
        public bool CanInteract { set { canInteract = value; } }    

        private bool IsSprinting => canSprint && Input.GetKey(sprintKey);
        private bool ShouldJump => Input.GetKeyDown(jumpKey) && characterController.isGrounded;
        private bool ShouldCrouch => Input.GetKeyDown(crouchKey) && !duringCrouchAnimation && characterController.isGrounded;

        public float moveSpeed = 5.0f;

        [Header("Functional Options:"), Tooltip("Select which features the FPCC should implement."), Space(10)]
        [SerializeField] private bool canPause = true;
        [SerializeField] private bool canSprint = true;
        [SerializeField] private bool canJump = true;
        [SerializeField] private bool canCrouch = true;
        [SerializeField] private bool canUseHeadBob = true;
        [SerializeField] private bool slideOnSlopes = true;
        [SerializeField] private bool canZoom = true;
        [SerializeField] private bool canInteract = true;
        [SerializeField] private bool useFootsteps = true;
        [SerializeField] private bool canClimb = true;
        


        [Header("Key Bindings:"), Space(10)]
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode crouchKey = KeyCode.C;
        [SerializeField] private KeyCode zoomKey = KeyCode.Mouse1;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;



        [Header("Movement Parameters"), Space(10)]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float gravity = 30f;
        [SerializeField] private float crouchSpeed;
        [SerializeField] private float slopeSpeed = 8f;


        [Header("Look Parameters:"), Space(10)]
        [SerializeField, Range(.1f, 3f)] private float sensitivityX = 2f;
        [SerializeField, Range(.1f, 3f)] private float sensitivityY = 2f;
        [SerializeField, Range(1f, 180f)] private float degreesUp = 80f;
        [SerializeField, Range(1f, 180f)] private float degreesDown = 80f;

        private Vector2 lookInput;

        [Header("Jump Parameters:"), Space(10)]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravityForce = 30f;

        [Header("Crouch Parameters:"), Space(10)]
        [SerializeField] private float crouchHeight = .6f;
        [SerializeField] private float standingHeight = 1.5f;
        [SerializeField] private float timeToCrouch = .25f;
        [SerializeField] private Vector3 crouchingCenter = new Vector3(0, .5f, 0);
        [SerializeField] private Vector3 standingCenter = new Vector3(0, 0, 0);

        private bool isCrouching;
        private bool duringCrouchAnimation;


        [Header("Headbob Parameters:"), Space(10)]
        [SerializeField] private float walkBobSpeed = 14f;
        [SerializeField] private float walkBobAmount = .05f;
        [SerializeField] private float sprintBobSpeed = 18f;
        [SerializeField] private float sprintBobAmount = .11f;
        [SerializeField] private float crouchBobSpeed = 8f;
        [SerializeField] private float crouchBobAmount = .025f;

        [Header("Zoom Parameters:"), Space(10)]
        [SerializeField] private float timeToZoom = .3f;
        [SerializeField] private float zoomFov = 30f;

        [Header("FPS Crosshair Parameters:"), Space(10)]
        [SerializeField] private Sprite normalCrosshair = null;
        [SerializeField] private Sprite detectedCrosshair = null;
        private bool setDetectedCrosshair;


        private float defaultFOV;
        private Coroutine zoomRoutine;

        private float defaultYPos = 0f;
        private float timer = 0f;

        private PlayerControls playerControls;


        // SLIDING PARAMETERS

        private Vector3 hitPointNormal;
        private bool IsSliding
        {

            get
            {

                if (characterController.isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 2f))
                {
                    hitPointNormal = slopeHit.normal;
                    return Vector3.Angle(hitPointNormal, Vector3.up) > characterController.slopeLimit;
                }
                else
                {
                    return false;
                }
            }

        }

        // INTERACTION PARAMETERS        

        private bool interfaces, abstractClasses;
        private bool doOnce;
        private bool checkOnce;

        [Header("Interaction Parameters:"), Space(10)]
        [SerializeField] private Vector3 interactionRaypoint = default;
        [SerializeField] private float detectionDistance = 3f;
        [SerializeField] private LayerMask interactableLayer = default;
        private Interactable currentInteractable;
        public InputAction Interact;

        [Header("Footstep Parameters:"), Space(10)]        
        [SerializeField] private float baseStepSpeed = .5f;
        [SerializeField] private float crouchStepMultiplier = 1.5f;
        [SerializeField] private float sprintStepMultiplier = .6f;
        private AudioSource footstepAudioSource = default;
        [SerializeField] private AudioClip[] forestGroundClips = default;
        [SerializeField] private AudioClip[] woodClips = default;
        [SerializeField] private AudioClip[] stoneClips = default;
        [SerializeField] private AudioClip[] metalClips = default;
        [SerializeField] private AudioClip[] tileClips = default;



        private float footstepTimer = 0f;
        private float GetCurrentOffset => isCrouching ? baseStepSpeed * crouchStepMultiplier : IsSprinting ? baseStepSpeed * sprintStepMultiplier : baseStepSpeed;


        [Header("Climbing Parameters")]
        private bool isClimbing;


        private Camera playerCamera;
        private CharacterController characterController;

        private Vector3 moveDir;
        private Vector2 currentInput;

        private float rotationX = 0;

        private Outline currentOutline;

        // PAUSE EVENT & TODO
                
        public event EventHandler OnPause;
        private bool isPaused;
        public bool IsPaused { get { return isPaused; } set { isPaused = value; } }



        #region Awake Start & Update

        private void Awake()
        {
            Instance = this;

            playerCamera = GetComponentInChildren<Camera>();
            characterController = GetComponent<CharacterController>();
            
            defaultFOV = playerCamera.fieldOfView;

            defaultYPos = playerCamera.transform.localPosition.y;

            footstepAudioSource = GetComponent<AudioSource>();

            if (inputSystem == InputSystem.newInputSystem)
            {
                playerControls = new PlayerControls();
                Interact = playerControls.Player.Interact;
            }

        }        


        private void Start()
        {
            switch (inputSystem)
            {
                case InputSystem.legacy:

                    print("LEGACY INPUT SYSTEM IN USE");

                    break;

                case InputSystem.newInputSystem:

                    playerControls.Player.Move.performed += ctx => currentInput = ctx.ReadValue<Vector2>();
                    playerControls.Player.Move.canceled += ctx => currentInput = Vector2.zero;

                    playerControls.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
                    playerControls.Player.Look.canceled += ctx => lookInput = Vector2.zero;

                                                            

                    //playerControls.Player.Flashlight.performed += FlashLightController.Instance.FlashLightAction;
      

                    if (canJump)
                    {
                        playerControls.Player.Jump.performed += HandleJump;
                    }

                    if (canCrouch)
                    {
                        playerControls.Player.Crouch.performed += HandleCrouch;
                    }

                    if (canPause)
                    {
                        playerControls.Player.Pause.performed += GameManager.Instance.HandlePause;
                    }


                    break;
            }




            if (canInteract)
            {
                switch (detectionSystem)
                {
                    case DetectionSystem.none:
                        {
                            break;
                        }

                    case DetectionSystem.interfaces:
                        {
                            interfaces = true;
                        }
                        break;
                    case DetectionSystem.abstractClasses:
                        {
                            abstractClasses = true;
                        }
                        break;
                }
            }
        }




        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1) && debugMode)
            {
                CanMove = !CanMove;
            }

            if (CanMove)
            {
                HandleMovementInput();
                HandleMouseLook();

                if (canJump)
                {
                    HandleJumpLegacy();
                }

                if (canCrouch)
                {
                    HandleCrouchLegacy();
                }

                if (canUseHeadBob)
                {
                    HandleHeadBob();
                }

                if (canZoom)
                {
                    HandleZoom();
                }

                ApplyFinalMovements();
            }

            if (interfaces && canInteract)
            {
                HandleInterfaceRaycast();
            }

            if (abstractClasses && canInteract)
            {
                HandleInteractionCheck();
                HandleInteractionInputLegacy();
            }

            if (useFootsteps)
            {
                HandleFootStepAudio();
            }

            if (canClimb && isClimbing)
            {
                CanMove = false;

                HandleClimbInput();

            }

            if (inputSystem == InputSystem.legacy)
            {
                if (Input.GetKeyDown(pauseKey) && canPause) { HandlePause(); }                
            }

        }

        /// <summary>
        /// Method to handle pause in game. May need dependencies to external scripts.
        /// </summary>
        public void HandlePause()
        {
            if (!isPaused)
            {
                CanMove = false;
                OnPause?.Invoke(this, EventArgs.Empty);
                isPaused = true;
            }
            else
            {
                CanMove = true;
                OnPause?.Invoke(this, EventArgs.Empty);
                isPaused = false;
            }
            

            /*if (Input.GetKeyDown(pressToPause) && !ExamineSystem.Instance.ExamineCanvasOpen && canPause)
            {
                GameManager.Instance?.HandlePauseLegacy();

            }

            if (Input.GetKeyUp(pressToPause) && ExamineSystem.Instance.ExamineCanvasOpen && canPause)
            {
                ExamineSystem.Instance?.OnExitCanvas();
            }*/
        }




        #endregion


        private void HandleMovementInput()
        {
            if (inputSystem == InputSystem.newInputSystem)
            {
                currentInput = playerControls.Player.Move.ReadValue<Vector2>();

                currentInput = new Vector2(currentInput.x, currentInput.y).normalized;

                // Multiply the input vector by the movement speed
                currentInput *= moveSpeed;

                float moveDirY = moveDir.y;

                // Calculate the move direction vector based on the transformed forward and right vectors
                moveDir = (transform.TransformDirection(Vector3.forward) * currentInput.y + transform.TransformDirection(Vector3.right) * currentInput.x);

                moveDir.y = moveDirY;
            }

            else if (inputSystem == InputSystem.legacy)
            {
                //WASD Settings

                currentInput = new Vector2((isCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed) * Input.GetAxis("Vertical"), (isCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed) * Input.GetAxis("Horizontal"));
                
                float moveDirY = moveDir.y;

                //MOVE TRANSFORM (THIS OBJECT)

                moveDir = (transform.TransformDirection(Vector3.forward) * currentInput.x) + (transform.TransformDirection(Vector3.right) * currentInput.y);
                moveDir.y = moveDirY;
            }
            
        }

        private void HandleMouseLook()
        {
            if (inputSystem == InputSystem.newInputSystem)
            {
                // UP AND DOWN

                rotationX -= lookInput.y * sensitivityY;

                rotationX = Mathf.Clamp(rotationX, -degreesUp, degreesDown);

                playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);

                //LEFT AND RIGHT

                transform.rotation *= Quaternion.Euler(0, lookInput.x * sensitivityX, 0);
            }
            else if (inputSystem == InputSystem.legacy)
            {
                // UP AND DOWN

                rotationX -= Input.GetAxis("Mouse Y") * sensitivityY;

                rotationX = Mathf.Clamp(rotationX, -degreesUp, degreesDown);

                playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);

                //LEFT AND RIGHT

                transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X"), 0);
            }

        }

        private void ApplyFinalMovements()
        {
            if (!characterController.isGrounded)
            {
                moveDir.y -= gravity * Time.deltaTime;
            }

            if (slideOnSlopes && IsSliding)
            {
                moveDir += new Vector3(hitPointNormal.x, -hitPointNormal.y, hitPointNormal.z) * slopeSpeed;
            }

            characterController.Move(moveDir * Time.deltaTime);
        }

        private void HandleJump(InputAction.CallbackContext ctx)
        {
            if (characterController.isGrounded)
            {
                moveDir.y = jumpForce;
            }
        }

        private void HandleJumpLegacy()
        {
            if (ShouldJump)
            {
                moveDir.y = jumpForce;
            }
        }

        private void HandleCrouch(InputAction.CallbackContext ctx)
        {
            if (!duringCrouchAnimation && characterController.isGrounded && canCrouch)
            {
                StartCoroutine(CrouchStand());
            }

        }

        private void HandleCrouchLegacy()
        {
            if (ShouldCrouch)
            {
                StartCoroutine(CrouchStand());
            }
        }

        private void HandleHeadBob()
        {
            if (!characterController.isGrounded) return;

            if (Mathf.Abs(moveDir.x) > .1f || Mathf.Abs(moveDir.z) > .1f)
            {
                timer += Time.deltaTime * (isCrouching ? crouchBobSpeed : IsSprinting ? sprintBobSpeed : walkBobSpeed);
                playerCamera.transform.localPosition = new Vector3(
                    playerCamera.transform.localPosition.x,
                    defaultYPos + Mathf.Sin(timer) * (isCrouching ? crouchBobAmount : IsSprinting ? sprintBobAmount : walkBobAmount),
                    playerCamera.transform.localPosition.z);
            }

        }

        private void HandleZoom()
        {
            if (Input.GetKeyDown(zoomKey))
            {
                if (zoomRoutine != null)
                {
                    StopCoroutine(zoomRoutine);
                    zoomRoutine = null;
                }

                zoomRoutine = StartCoroutine(ToggleZoom(true));
            }

            if (Input.GetKeyUp(zoomKey))
            {
                if (zoomRoutine != null)
                {
                    StopCoroutine(zoomRoutine);
                    zoomRoutine = null;
                }

                zoomRoutine = StartCoroutine(ToggleZoom(false));
            }
        }

        private IEnumerator ToggleZoom(bool isEnter)
        {
            float targetFOV = isEnter ? zoomFov : defaultFOV;
            float startingFOV = playerCamera.fieldOfView;
            float timeElapsed = 0;

            while (timeElapsed < timeToZoom)
            {
                playerCamera.fieldOfView = Mathf.Lerp(startingFOV, targetFOV, timeElapsed / timeToZoom);
                timeElapsed += Time.deltaTime;
                yield return null;
            }

            playerCamera.fieldOfView = targetFOV;
            zoomRoutine = null;
        }

        private IEnumerator CrouchStand()
        {
            if (isCrouching && Physics.Raycast(playerCamera.transform.position, Vector3.up, 1f))
            {
                yield break;
            }

            
            duringCrouchAnimation = true;

            float timeElapsed = 0f;

            float targetHeight = isCrouching ? standingHeight : crouchHeight;
            float currentHeight = characterController.height;
            Vector3 targetCenter = isCrouching ? standingCenter : crouchingCenter;
            Vector3 currentCenter = characterController.center;

            

            while (timeElapsed < timeToCrouch)
            {
                characterController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / timeToCrouch);
                characterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / timeToCrouch);
                timeElapsed += Time.deltaTime;
                yield return null;
            }

            characterController.height = targetHeight;
            characterController.center = targetCenter;

            isCrouching = !isCrouching;

            /*if (isCrouching) TODO
            {
                PostProcessingController.Instance?.OnCrouch();
            }
            else if (!isCrouching)
            {
                PostProcessingController.Instance?.OnUnCrouch();
            }*/

            duringCrouchAnimation = false;
        }

        private void HandleInterfaceRaycast()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, detectionDistance, interactableLayer);


            if (hasHit && !TextBoxController.Instance.ShowingText)            {
                

                if (hit.collider.TryGetComponent(out IInteractive interactable))
                {
                    if (!setDetectedCrosshair)
                    {
                        CrosshairController.Instance?.SetCrosshair(true, Vector3.zero, detectedCrosshair, true);
                        setDetectedCrosshair = true;
                    }
                    

                    doOnce = false;

                    if (inputSystem == InputSystem.legacy)
                    {
                        if (Keyboard.current.eKey.wasPressedThisFrame)
                        {
                            interactable.Interact();
                        }
                    }

                    if (inputSystem == InputSystem.newInputSystem && !checkOnce)
                    {

                        playerControls.Player.Interact.performed += ctx => interactable.Interact();


                        checkOnce = true;
                    }


                }

                if (hit.collider.TryGetComponent(out Outline outline))
                {
                    currentOutline = outline;
                    outline.enabled = true;
                }


                else
                {
                    if (inputSystem == InputSystem.newInputSystem)
                    {
                        playerControls.Player.Interact.performed -= ctx => interactable.Interact();
                    }

                    

                    checkOnce = false;
                }


            }

            else
            {
                if (!doOnce)
                {                    
                    CrosshairController.Instance?.SetCrosshair(true, Vector3.zero, normalCrosshair, false);
                    setDetectedCrosshair = false;
                    doOnce = true;
                    if (currentOutline == null) { return; }
                    else
                    {
                        currentOutline.enabled = false;
                        currentOutline = null;
                        
                    }                                      
                    

                    
                }

            }
        }

        private void HandleInteractionCheck()
        {
            if (Physics.Raycast(playerCamera.ViewportPointToRay(interactionRaypoint), out RaycastHit hit, detectionDistance))
            {
                if (hit.collider.gameObject.layer == 6 && (currentInteractable == null) || hit.collider.gameObject.GetInstanceID() != currentInteractable.GetInstanceID())
                {
                    hit.collider.TryGetComponent(out currentInteractable);

                    if (currentInteractable != null)
                    {
                        currentInteractable.OnFocus();
                    }

                }
            }
            else if (currentInteractable)
            {
                currentInteractable.OnLoseFocus();
                currentInteractable = null;
            }
        }
        private void HandleInteractionInput()
        {
            if (currentInteractable != null && Physics.Raycast(playerCamera.ViewportPointToRay(interactionRaypoint), out RaycastHit hit, detectionDistance, interactableLayer))
            {
                currentInteractable.OnInteract();
            }
        }

        private void HandleInteractionInputLegacy()
        {
            if (Input.GetKeyDown(interactKey) && currentInteractable != null && Physics.Raycast(playerCamera.ViewportPointToRay(interactionRaypoint), out RaycastHit hit, detectionDistance, interactableLayer))
            {
                currentInteractable.OnInteract();
            }
        }

        private void HandleFootStepAudio()
        {
            if (!characterController.isGrounded) return;
            if (currentInput == Vector2.zero) return;
            if (!CanMove) return;

            footstepTimer -= Time.deltaTime;

            if (footstepTimer <= 0)
            {
                
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5))
                {
                    switch (hit.collider.tag)
                    {
                        case "Footsteps/FOREST":

                            PlayFootStepAudioClips(forestGroundClips, 1f);
                            break;

                        case "Footsteps/WOOD":

                            PlayFootStepAudioClips(woodClips, 1f);
                            break;

                        case "Footsteps/STONE":

                            PlayFootStepAudioClips(stoneClips, 1f);
                            break;

                        case "Footsteps/METAL":

                            PlayFootStepAudioClips(metalClips, 1f);
                            break;

                        case "Footsteps/TILE":
                            PlayFootStepAudioClips(tileClips, .3f);
                            break;

                        default:

                            PlayFootStepAudioClips(woodClips, 1f);
                            print("Default footstep audio called");
                                                        
                            break;
                    }
                }

                footstepTimer = GetCurrentOffset;
            }

        }

        private void HandleClimbInput()
        {
            //WASD Settings

            currentInput = new Vector2(Input.GetAxis("Vertical"), Input.GetAxis("Horizontal"));

            float moveDirY = moveDir.y;

            //MOVE TRANSFORM (THIS OBJECT)

            moveDir = (transform.TransformDirection(Vector3.up) * currentInput.x) + (transform.TransformDirection(Vector3.right) * currentInput.y);
            moveDir.y = moveDirY;
        }

        public void PlayerClimb()
        {
            isClimbing = true;
        }

        public Sprite GetDefaultCrosshair()
        {
            return normalCrosshair;
        }

        private void PlayFootStepAudioClips(AudioClip[] footstepAudioClip, float volume)
        {
            int randomClip = Random.Range(0, footstepAudioClip.Length -1);            

            footstepAudioSource.volume = volume;

            //print("Playing footstep audioclip " + randomClip + " called " + footstepAudioClip[randomClip] + " at " + volume);
            footstepAudioSource?.PlayOneShot(footstepAudioClip[randomClip]);
            
        }

        private void OnDestroy()
        {
            if (inputSystem == InputSystem.newInputSystem)
            {
                playerControls.Player.Move.performed -= ctx => currentInput = ctx.ReadValue<Vector2>();
                playerControls.Player.Move.canceled -= ctx => currentInput = Vector2.zero;

                playerControls.Player.Look.performed -= ctx => lookInput = ctx.ReadValue<Vector2>();
                playerControls.Player.Look.canceled -= ctx => lookInput = Vector2.zero;

                playerControls.Player.Pause.performed -= GameManager.Instance.HandlePause;

                //playerControls.Player.Flashlight.performed += FlashLightController.Instance.SetFlashlight(true);

                print(gameObject.name + " destroyed!");
            }            
        }

        private void OnEnable()
        {
            if (inputSystem == InputSystem.newInputSystem) { playerControls.Player.Enable(); }
        }

        private void OnDisable()
        {
            if (inputSystem == InputSystem.newInputSystem) { playerControls.Player.Disable(); }
        }


    }
}