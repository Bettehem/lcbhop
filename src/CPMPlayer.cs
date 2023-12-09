/* Based on https://github.com/WiggleWizard/quake3-movement-unity3d/blob/master/CPMPlayer.cs
 * Modified to match https://github.com/ValveSoftware/halflife/blob/master/pm_shared/pm_shared.c
 */

using System.Collections;
using System.Collections.Generic;

using GameNetcodeStuff;

using lcbhop;

using UnityEngine;

// Contains the command the user wishes upon the character
struct Cmd {
    public float forwardMove;
    public float rightMove;
    public float upMove;
}

public class CPMPlayer : MonoBehaviour {
    /*Frame occuring factors*/
    public float gravity = 20.0f;

    public float friction = 4.0f;                 //Ground friction

    /* Movement stuff */
    public float maxspeed = 8.0f;                 // Max speed
    public float accelerate = 10.0f;              // Ground accel
    public float stopspeed = 10.0f;               // Deacceleration that occurs when running on the ground
    public float airaccelerate = 100.0f;          // Air accel
    public bool holdJumpToBhop = true;            // When enabled allows player to just hold jump button to keep on bhopping perfectly. Beware: smells like casual.

    public PlayerControllerB player;
    private CharacterController _controller;

    private Vector3 playerVelocity = Vector3.zero;

    private bool wishJump = false;

    // Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)
    private Cmd _cmd;

    private void Start( ) {
        _controller = player.thisController;
    }

    private void Update( ) {
        if ( ( !player.IsOwner || !player.isPlayerControlled || ( player.IsServer && !player.isHostPlayerObject ) ) && !player.isTestingPlayer ) {
            return;
        }
        if ( player.quickMenuManager.isMenuOpen || player.inSpecialInteractAnimation || player.isTypingChat ) {
            return;
        }

        // Don't patch movement on ladders
        if ( player.isClimbingLadder ) {
            Plugin.patchMove = false;
            return;
        }

        // Allow crouching while mid air
        player.fallValue = 0.0f;
        // Disables fall damage
        player.fallValueUncapped = 0.0f;

        /* Movement, here's the important part */
        QueueJump( );

        if ( _controller.isGrounded )
            Friction( );

        if ( _controller.isGrounded )
            WalkMove( );
        else if ( !_controller.isGrounded )
            AirMove( );

        // Move the controller
        Plugin.patchMove = false; // Disable the Move Patch
        _controller.Move( playerVelocity * Time.deltaTime );
        Plugin.patchMove = true; // Reenable the Move Patch
    }

    /*******************************************************************************************************\
   |* MOVEMENT
   \*******************************************************************************************************/

    /**
     * Sets the movement direction based on player input
     */
    private void SetMovementDir( ) {
        _cmd.forwardMove = player.playerActions.Movement.Move.ReadValue<Vector2>( ).y;
        _cmd.rightMove = player.playerActions.Movement.Move.ReadValue<Vector2>( ).x;
    }

    /**
     * Queues the next jump just like in Q3
     */
    private void QueueJump( ) {
        if ( holdJumpToBhop ) {
            wishJump = player.playerActions.Movement.Jump.ReadValue<float>( ) > 0.0f;
            return;
        }
    }

    /**
     * Execs when the player is in the air
    */
    private void AirMove( ) {
        Vector3 wishvel;
        Vector3 wishdir;
        float wishspeed;

        SetMovementDir( );

        wishvel = new Vector3( _cmd.rightMove, 0, _cmd.forwardMove );
        wishvel = transform.TransformDirection( wishvel );

        wishdir = wishvel;

        wishspeed = wishdir.magnitude;
        wishdir.Normalize( );

        if ( wishspeed > maxspeed ) {
            wishvel *= maxspeed / wishspeed;
            wishspeed = maxspeed;
        }

        AirAccelerate( wishdir, wishspeed, airaccelerate );

        // Apply gravity
        playerVelocity.y -= gravity * Time.deltaTime;
    }

    /**
     * Called every frame when the engine detects that the player is on the ground
     */
    private void WalkMove( ) {
        Vector3 wishvel;
        Vector3 wishdir;
        float wishspeed;

        SetMovementDir( );

        wishvel = new Vector3( _cmd.rightMove, 0, _cmd.forwardMove );
        wishvel = transform.TransformDirection( wishvel );

        wishdir = wishvel;

        wishspeed = wishdir.magnitude * maxspeed;
        wishdir.Normalize( );

        if ( wishspeed > maxspeed ) {
            wishvel *= maxspeed / wishspeed;
            wishspeed = maxspeed;
        }

        Accelerate( wishdir, wishspeed, accelerate );

        // Reset the gravity velocity
        playerVelocity.y = -gravity * Time.deltaTime;

        if ( wishJump ) {
            playerVelocity.y = 8.0f;
            wishJump = false;
        }
    }

    /**
     * Applies friction to the player, called in both the air and on the ground
     */
    private void Friction( ) {
        Vector3 vec = playerVelocity;
        float speed;
        float newspeed;
        float control;
        float drop;

        vec.y = 0.0f;
        speed = vec.magnitude;

        if ( speed < 0.1f )
            return;

        drop = 0.0f;

        /* Only if the player is on the ground then apply friction */
        if ( _controller.isGrounded ) {
            control = ( speed < stopspeed ) ? stopspeed : speed;
            drop += control * friction * Time.deltaTime;
        }

        newspeed = speed - drop;
        if ( newspeed < 0 )
            newspeed = 0;

        newspeed /= speed;

        playerVelocity.x *= newspeed;
        playerVelocity.z *= newspeed;
    }

    private void Accelerate( Vector3 wishdir, float wishspeed, float accel ) {
        float addspeed;
        float accelspeed;
        float currentspeed;

        currentspeed = Vector3.Dot( playerVelocity, wishdir );

        addspeed = wishspeed - currentspeed;

        if ( addspeed <= 0 )
            return;

        accelspeed = accel * Time.deltaTime * wishspeed;

        if ( accelspeed > addspeed )
            accelspeed = addspeed;

        playerVelocity.x += accelspeed * wishdir.x;
        playerVelocity.z += accelspeed * wishdir.z;
    }

    private void AirAccelerate( Vector3 wishdir, float wishspeed, float accel ) {
        float addspeed;
        float accelspeed;
        float currentspeed;
        float wishspd = wishspeed;

        if ( wishspd > 30 )
            wishspd = 30;

        currentspeed = Vector3.Dot( playerVelocity, wishdir );

        addspeed = wishspd - currentspeed;

        if ( addspeed <= 0 )
            return;

        accelspeed = accel * wishspeed * Time.deltaTime;

        if ( accelspeed > addspeed )
            accelspeed = addspeed;

        playerVelocity.x += accelspeed * wishdir.x;
        playerVelocity.z += accelspeed * wishdir.z;
    }
}
