using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEditor.XR.LegacyInputHelpers;
using UnityEngine.XR;
using UnityEngine.InputSystem;

public class HexaBody : MonoBehaviour {
    [Header("XR Rig")]
    public GameObject PlayerController;
    public XRRig XRRig;
    public GameObject XRCamera;
    public GameObject CameraOffset;

    [Header("Controllers")]
    public ActionBasedController CameraController;
    public ActionBasedController RightHandController;
    public ActionBasedController LeftHandController;

    public InputActionReference RightTrackPadPress;
    public InputActionReference RightTrackPadTouch;
    public InputActionReference LeftTrackPadPress;
    public InputActionReference LeftTrackPadTouch;

    [Header("Hexabody")]
    public GameObject Body;
    public GameObject Head;
    public GameObject Chest;
    public GameObject Fender;
    public GameObject Sphere;
    public GameObject RightHand;
    public GameObject LeftHand;

    public ConfigurableJoint RightHandJoint;
    public ConfigurableJoint LeftHandJoint;
    public ConfigurableJoint Spine;

    [Header("Movement")]
    public float turnSpeed = 3;
    public float moveForceCrouch = 15;
    public float moveForceWalk = 30;
    public float moveForceSprint = 45;

    [Header("Drag")]
    public float angularDragOnMove = 40;
    public float angularBreakDrag = 100;

    [Header("Crouch and Jump")]
    public float crouchSpeed = 1f;
    public float jumpSpeed = 1.2f;
    public float minCrouch = 0.3f;
    public float maxCrouch = 1.8f;
    private float additionalHeight;
    public Vector3 crouchTarget;

    bool jumping = false;
    bool moving = false;

    // Input
    private Vector3 cameraControllerPosition;
    private Vector3 rightHandControllerPosition;
    private Vector3 leftHandControllerPosition;

    private Quaternion cameraControllerRotation;
    private Quaternion rightHandControllerRotation;
    private Quaternion leftHandControllerRotation;

    private Vector2 RightTrackpad;
    private Vector2 LeftTrackpad;

    private float RightTrackpadPressed;
    private float LeftTrackpadPressed;

    private float RightTrackpadTouched;
    private float LeftTrackpadTouched;

    private Quaternion headYaw;
    private Vector3 moveDirection;
    private Vector3 sphereTorque;
    private Vector3 bodyOffset;

    void Start() {
        additionalHeight = (0.5f * Sphere.transform.lossyScale.y) + (0.5f * Fender.transform.lossyScale.y) + (Head.transform.position.y - Chest.transform.position.y);
    }

    // On every physics update
    private void FixedUpdate() {
        GetControllerInputs();
        CalculateValues();
        MoveAndRotateHands();
        MoveAndRotateBody();
        RigToBody();
        Jump();
        if (!jumping) PhysicalCrouch();
        // Debugs();
    }

    private void Debugs() {
        Debug.Log("Jumping: "+ jumping);
        Debug.Log("Moving: "+ moving);
        Debug.Log("BODY OFFSET: "+ bodyOffset);
    }

    // Gets controller inputs
    private void GetControllerInputs() {
        // Right Controller Position & Rotation
        rightHandControllerPosition = RightHandController.positionAction.action.ReadValue<Vector3>();
        rightHandControllerRotation = RightHandController.rotationAction.action.ReadValue<Quaternion>();
        // Trackpad
        RightTrackpad = RightHandController.translateAnchorAction.action.ReadValue<Vector2>();
        RightTrackpadPressed = RightTrackPadPress.action.ReadValue<float>();
        RightTrackpadTouched = RightTrackPadTouch.action.ReadValue<float>();

        // Left Contoller Position & Rotation
        leftHandControllerPosition = LeftHandController.positionAction.action.ReadValue<Vector3>();
        leftHandControllerRotation = LeftHandController.rotationAction.action.ReadValue<Quaternion>();
        // Trackpad
        LeftTrackpad = LeftHandController.translateAnchorAction.action.ReadValue<Vector2>();
        LeftTrackpadPressed = LeftTrackPadPress.action.ReadValue<float>();
        LeftTrackpadTouched = LeftTrackPadTouch.action.ReadValue<float>();

        // Camera Inputs
        cameraControllerPosition = CameraController.positionAction.action.ReadValue<Vector3>();
        cameraControllerRotation = CameraController.rotationAction.action.ReadValue<Quaternion>();
    }

    // Calculates body and movement values
    private void CalculateValues() {
        // Values
        headYaw = Quaternion.Euler(0, XRCamera.transform.eulerAngles.y, 0);
        moveDirection = headYaw * new Vector3(LeftTrackpad.x, 0, LeftTrackpad.y);
        sphereTorque = new Vector3(moveDirection.z, 0, -moveDirection.x);
        // bodyOffset.x = Body.transform.position.x - XRRig.transform.position.x;
        // bodyOffset.z = Body.transform.position.z - XRRig.transform.position.z;
    }

    // Camera and Rig stuff
    private void RigToBody() {
        // Roomscale
        Body.transform.position = new Vector3(CameraController.transform.position.x, Body.transform.position.y, CameraController.transform.position.z);
        XRCamera.transform.rotation = CameraController.transform.rotation;

        // Body.transform.position = cameraControllerPosition;
        // XRCamera.transform.position = Head.transform.position;
        // XRRig.transform.position = new Vector3(Fender.transform.position.x, Fender.transform.position.y - (0.5f * Fender.transform.localScale.y + 0.5f * Sphere.transform.localScale.y), Fender.transform.position.z);
    }

    // Movement
    private void MoveAndRotateBody() {
        RotateBody();
        MoveBody();
    }

    // Rotates Rig AND Body
    private void RotateBody() {
        if (RightTrackpadPressed == 1) return;
        Head.transform.Rotate(0, RightTrackpad.x * turnSpeed, 0, Space.Self);
        // Body.transform.Rotate(0, RightTrackpad.x * turnSpeed, 0, Space.Self);
        // PlayerController.transform.Rotate(0, RightTrackpad.x * turnSpeed, 0, Space.Self);
        XRRig.transform.RotateAround(Body.transform.position, Vector3.up, RightTrackpad.x * turnSpeed);
        Chest.transform.rotation = headYaw;
    }
    
    // Sphere control
    private void MoveBody() {
        if (LeftTrackpadTouched == 0) StopSphere();
        if (LeftTrackpadTouched == 1 && LeftTrackpadPressed == 0) MoveSphere(moveForceWalk);
        if (LeftTrackpadTouched == 1 && LeftTrackpadPressed == 1) MoveSphere(moveForceSprint);
        if (jumping && LeftTrackpadTouched == 1) MoveSphere(moveForceCrouch);
    }

    // Add torque to sphere for body movement
    private void MoveSphere(float force) {
        Sphere.GetComponent<Rigidbody>().freezeRotation = false;
        moving = true;
        Sphere.GetComponent<Rigidbody>().angularDrag = angularDragOnMove;
        Sphere.GetComponent<Rigidbody>().AddTorque(sphereTorque.normalized * (force * 2), ForceMode.Force);
    }

    // Stops sphere and freezes its rotation
    private void StopSphere() {
        Sphere.GetComponent<Rigidbody>().angularDrag = angularBreakDrag;
        if (Sphere.GetComponent<Rigidbody>().velocity == Vector3.zero) Sphere.GetComponent<Rigidbody>().freezeRotation = true;
        moving = false;
    }

    // Jump
    private void Jump() {
        if (RightTrackpadPressed == 1) JumpPreload();
        if (RightTrackpadPressed == 0 && jumping == true) JumpRelease();
    }

    // Virtual crouch for jump
    private void JumpPreload() {
        jumping = true;
        crouchTarget.y = Mathf.Clamp(crouchTarget.y -= crouchSpeed * Time.fixedDeltaTime, minCrouch, maxCrouch);
        Spine.targetPosition = new Vector3(0, crouchTarget.y, 0);
    }

    // Virtual crouch release for jump
    private void JumpRelease() {
        jumping = false;
        crouchTarget.y = Mathf.Clamp(crouchTarget.y += jumpSpeed * Time.fixedDeltaTime, minCrouch, maxCrouch);
        Spine.targetPosition = new Vector3(0, crouchTarget.y, 0);
    }

    // Physical crouch
    private void PhysicalCrouch() {
        crouchTarget.y = Mathf.Clamp(cameraControllerPosition.y - additionalHeight, minCrouch, maxCrouch - additionalHeight);
        Spine.targetPosition = new Vector3(0, crouchTarget.y, 0);
    }

    // Moves and rotates hands with a target
    private void MoveAndRotateHands() {
        RightHandJoint.targetPosition = rightHandControllerPosition - cameraControllerPosition;
        LeftHandJoint.targetPosition = leftHandControllerPosition - cameraControllerPosition;
        RightHandJoint.targetRotation = rightHandControllerRotation;
        LeftHandJoint.targetRotation = leftHandControllerRotation;
    }
}