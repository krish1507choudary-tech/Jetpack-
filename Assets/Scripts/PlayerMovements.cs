
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovements : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 10f;

    [Header("Rotation")]
    [SerializeField] private float rotationSmoothTime = 0.12f;

    [Header("Gravity")]
    [SerializeField] private float gravity = 20f;

    [Header("Jetpack")]
    [SerializeField] private float jetpackThrust = 18f;
    [SerializeField] private float maxUpwardSpeed = 5f;
    [SerializeField] private float jetpackLaunchImpulse = 2f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private Vector2 moveInput;
    private bool sprintPressed;
    private bool spaceHeld;

    private float currentSpeed;
    private float verticalVelocity;
    private float rotationVelocity;

    private bool launchAnimationPlaying;
    private bool jetpackActive;
    private bool jetpackUsedThisFlight;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int SprintHash =
        Animator.StringToHash("IsSprinting");

    private static readonly int JetpackLaunchHash =
        Animator.StringToHash("JetpackLaunch");

    private const string LaunchStateName = "Jump launch";
    private const string LevitateStateName = "Levitate_Idle";

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        ReadInput();
        HandleJetpackInput();
        MovePlayer();
        UpdateAnimations();
    }

    private void ReadInput()
    {
        moveInput = Vector2.zero;
        sprintPressed = false;
        spaceHeld = false;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.wKey.isPressed ||
            Keyboard.current.upArrowKey.isPressed)
            moveInput.y += 1f;

        if (Keyboard.current.sKey.isPressed ||
            Keyboard.current.downArrowKey.isPressed)
            moveInput.y -= 1f;

        if (Keyboard.current.aKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed)
            moveInput.x -= 1f;

        if (Keyboard.current.dKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed)
            moveInput.x += 1f;

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        sprintPressed = Keyboard.current.leftShiftKey.isPressed;
        spaceHeld = Keyboard.current.spaceKey.isPressed;
    }

    private void HandleJetpackInput()
    {
        if (Keyboard.current == null || animator == null)
            return;

        bool grounded = controller.isGrounded;

        // Allow another launch only after landing.
        if (grounded && !launchAnimationPlaying)
        {
            jetpackUsedThisFlight = false;
            jetpackActive = false;
        }

        // Start one launch per grounded attempt.
        if (Keyboard.current.spaceKey.wasPressedThisFrame &&
            grounded &&
            !jetpackUsedThisFlight)
        {
            jetpackUsedThisFlight = true;
            launchAnimationPlaying = true;
            jetpackActive = false;

            verticalVelocity = jetpackLaunchImpulse;
            animator.ResetTrigger(JetpackLaunchHash);
            animator.SetTrigger(JetpackLaunchHash);
        }

        if (!launchAnimationPlaying)
            return;

        AnimatorStateInfo stateInfo =
            animator.GetCurrentAnimatorStateInfo(0);

        bool inLaunchState =
            stateInfo.IsName(LaunchStateName);

        // Wait until the launch state actually starts.
        if (!inLaunchState)
            return;

        // Enable thrust only after the launch animation finishes.
        if (!animator.IsInTransition(0) &&
            stateInfo.normalizedTime >= 0.95f)
        {
            launchAnimationPlaying = false;
            jetpackActive = spaceHeld;
        }
    }

    private void MovePlayer()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Vector3 forward = cameraTransform != null
            ? cameraTransform.forward
            : Vector3.forward;

        Vector3 right = cameraTransform != null
            ? cameraTransform.right
            : Vector3.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            forward * moveInput.y +
            right * moveInput.x;

        direction = Vector3.ClampMagnitude(direction, 1f);

        bool moving = moveInput.sqrMagnitude > 0.01f;

        float targetSpeed = moving
            ? (sprintPressed ? sprintSpeed : walkSpeed)
            : 0f;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            (moving ? acceleration : deceleration) *
            Time.deltaTime
        );

        if (moving)
        {
            float targetAngle =
                Mathf.Atan2(direction.x, direction.z) *
                Mathf.Rad2Deg;

            float angle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref rotationVelocity,
                rotationSmoothTime
            );

            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        bool grounded = controller.isGrounded;

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        // Hold Space for upward thrust after launch.
        if (jetpackActive && spaceHeld && !grounded)
        {
            verticalVelocity += jetpackThrust * Time.deltaTime;
            verticalVelocity = Mathf.Min(
                verticalVelocity,
                maxUpwardSpeed
            );
        }
        else if (!grounded)
        {
            // Releasing Space allows gravity to pull the player down.
            verticalVelocity -= gravity * Time.deltaTime;
        }

        Vector3 horizontalVelocity = direction * currentSpeed;
        Vector3 finalVelocity =
            horizontalVelocity + Vector3.up * verticalVelocity;

        controller.Move(finalVelocity * Time.deltaTime);

        // Lock Space until the controller detects landing.
        if (controller.isGrounded && !launchAnimationPlaying)
        {
            jetpackActive = false;
            verticalVelocity = -2f;
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null)
            return;

        bool moving = moveInput.sqrMagnitude > 0.01f;
        bool sprinting = moving && sprintPressed;

        float animationSpeed = 0f;

        if (moving)
        {
            animationSpeed = sprinting
                ? 1f
                : Mathf.Clamp01(currentSpeed / walkSpeed);
        }

        animator.SetFloat(SpeedHash, animationSpeed);
        animator.SetBool(SprintHash, sprinting);
    }
}
