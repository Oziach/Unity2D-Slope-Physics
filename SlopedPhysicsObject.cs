using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]

public class SlopedPhysicsObject : MonoBehaviour
{

    private Rigidbody2D rb; //kinematic rigidbody
    private Vector2 newPosition, prevPosition;
    private float minMoveDistance = 0.001f;
    private const float slopeConstant = 0.005f;
  

    [SerializeField] private bool preserveXVelDownslope = true;
    [SerializeField] private float downslopeVelMultiplier = 1.25f;
    [SerializeField] public float maxClimbableSlopeAngle;
    [SerializeField] private float slopeSnapYVelThreshold;
    [SerializeField] public float velocityResolutionConstant = 0.5f;
    [SerializeField] public float gravityMultiplier = 1f;
  

    public Info info;
    protected ContactFilter2D contactFilter;
    private List<RaycastHit2D> hitsList = new List<RaycastHit2D>(4);

    [SerializeField] public Vector2 velocity;



    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.isKinematic = true;
        prevPosition = rb.position;

        contactFilter.useTriggers = false;
        contactFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        contactFilter.useLayerMask = true;

        SetInfo(Vector2.zero);
        info.xDistMoved = 0;
        info.movingIntoClimbable = false;
    }

    void OnEnable()
    {


    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        FixedUpdateEqv(); //custom fixed update

        prevPosition = rb.position;
        HandleGreatSlope();

        //x movement
        Vector2 deltaPosition;
        deltaPosition.x = velocity.x * Time.fixedDeltaTime;
        Vector2 moveAlongGround = info.groundNormalPerp;
        HandleDownSlopeVel(ref moveAlongGround);
        MoveX(deltaPosition.x * moveAlongGround);

        //y movement
        velocity += gravityMultiplier * Time.fixedDeltaTime * Physics2D.gravity;
        deltaPosition.y = velocity.y * Time.fixedDeltaTime;
        MoveY(deltaPosition.y * Vector2.up);

        //SnapToSlope(); //Change this if possible. This is a little limiting for map design. Not much, but still.
        SnapSlope(moveAlongGround * Mathf.Sign(velocity.x), info.xDistMoved);

        newPosition = rb.position;
        rb.position = prevPosition;
        rb.MovePosition(newPosition);

    }

    virtual protected void FixedUpdateEqv()
    {
        //in the custom physics component
    }

    void MoveX(Vector2 move) //move = moveAlongGround * deltaPosition.x for MoveX
    {
        float distance = move.magnitude;

        if (distance > minMoveDistance) //for optimization
        {

            hitsList.Clear(); //reset collisions list
            int count = rb.Cast(move.normalized, contactFilter, hitsList, distance + Physics2D.defaultContactOffset);

            for (int i = 0; i < count; i++)
            {
                Vector2 currentNormal = hitsList[i].normal;
                if (Vector2.Dot(currentNormal, move) < 0)
                {
                    float slopeAngle = Vector2.Angle(currentNormal, Vector2.up);

                    if (slopeAngle <= maxClimbableSlopeAngle)
                    {
                        SetInfo(hitsList[0].normal);
                        info.movingIntoClimbable = true;
                    }
                    else if (slopeAngle > maxClimbableSlopeAngle && slopeAngle <= 90)
                    {
                        velocity.x = 0;
                    }
                    else //if slope angle > 90
                    {
                        currentNormal.y *= velocityResolutionConstant;

                        float projectionValue = Vector2.Dot(velocity, currentNormal);
                        Vector2 projectionVector = currentNormal * projectionValue;
                        if (projectionValue < 0)
                        {
                            velocity -= projectionVector;
                        }
                    }

                    float modifiedDistance = hitsList[i].distance - Physics2D.defaultContactOffset;
                    distance = modifiedDistance < distance ? modifiedDistance : distance;
                }
            }

            info.xDistMoved = distance;
            rb.position += distance * move.normalized;
        }
    }

    void MoveY(Vector2 move)
    {
        float distance = move.magnitude;

        hitsList.Clear();
        int count = rb.Cast(move.normalized, contactFilter, hitsList,
            distance + 0.005f + Physics2D.defaultContactOffset); //I didn't want to add that 0.005f either.

        if (!info.movingIntoClimbable)
        {
            if (count == 0) { SetInfo(Vector2.zero); }
        }


        for (int i = 0; i < count; i++)
        {
            Vector2 currentNormal = hitsList[i].normal;

            if (Vector2.Dot(currentNormal, move) < 0) //HAHAHAHAHAHAHAHA, CHEW ON THAT! Try giving me physically impossible normals now, scum! EDIT: oh my god that floating point error mess almost gave me a heart attack //EDIT2: Thank you floating point error, that was terribly wrong logic before on my end
            {

                if (!info.movingIntoClimbable)
                {

                    if (Vector2.Angle(currentNormal, Vector2.up) < 90)
                    {
                        SetInfo(currentNormal);
                    }

                    else
                    {
                        velocity.y = 0;
                        SetInfo(Vector2.zero);
                    }

                }

                if (info.slopeAngle > maxClimbableSlopeAngle)
                {
                    float projectionValue = Vector2.Dot(velocity, currentNormal);
                    Vector2 projectionVector = currentNormal * projectionValue;
                    if (projectionValue < 0)
                    {
                        velocity -= projectionVector;
                    }
                }


                float modifiedDistance = hitsList[i].distance - Physics2D.defaultContactOffset;
                distance = modifiedDistance < distance ? modifiedDistance : distance;
            }
        }

        info.movingIntoClimbable = false;
        rb.position += distance * move.normalized;

    }

    void HandleGreatSlope() //if current surface angle is greater than specified max value
    {

        Vector2 slopeNormalPerp = -Vector2.Perpendicular(info.unclimbableGroundNormal);
        float multiplyFactor = (1 / slopeNormalPerp.y);
        float maxXVel = Mathf.Abs(velocity.y * multiplyFactor * slopeNormalPerp.x);

        //if(velocity.y == 0) { velocity.y = -0.1f; }

        if (info.unclimbableGroundNormal.x > 0)
        {
            if (velocity.y <= 0) { if (velocity.x < maxXVel) { velocity.x = maxXVel; } }
        }
        else if (info.unclimbableGroundNormal.x < 0)
        {
            if (velocity.y <= 0) { if (velocity.x > -maxXVel) { velocity.x = -maxXVel; } }
        }

    }



    void SnapSlope(Vector2 directedMoveAlongGround, float xDistMoved)
    {
        if (info.slopeAngle != info.prevSlopeAngle && info.slopeAngle == -1 && velocity.y <= slopeSnapYVelThreshold)
            //if went aerial this frame
        {
            Vector2 originalPos = rb.position;

            rb.position -= new Vector2(0, Physics2D.defaultContactOffset + 0.1f); //move down by slope constant
                                                                                               
            hitsList.Clear(); //reset collisions list
            int count = rb.Cast(-directedMoveAlongGround.normalized, contactFilter, hitsList, xDistMoved + Physics2D.defaultContactOffset);

            if (count != 0) //if hit/collision
            {

                if (hitsList[0].distance >= 0) //tiny value to avoid floating point errors
                {
                    float snapSlopeAngle = Vector2.Angle(hitsList[0].normal, Vector2.up);
                    Vector2 snapSlopeNormalPerp = -Vector2.Perpendicular(hitsList[0].normal);
                    Debug.Log(snapSlopeNormalPerp + " " + info.prevGroundNormalPerp);
                    if (snapSlopeAngle <= maxClimbableSlopeAngle && snapSlopeNormalPerp != info.prevGroundNormalPerp)
                    {
                        rb.position += -directedMoveAlongGround * (hitsList[0].distance - Physics2D.defaultContactOffset);
                        SetInfo(hitsList[0].normal); //update state
                        return;
                    }
                }
            }

            //if really aerial, restore position and velocity
            {
                rb.position = originalPos;
                velocity.y += info.prevGroundNormalPerp.y * velocity.x;
            }
        }
    }

    void HandleDownSlopeVel(ref Vector2 moveAlongGround)
    {

        if (moveAlongGround.y * velocity.x < -0.0001f)
        {
            if (preserveXVelDownslope)
            {
                float multiplier = 1 / moveAlongGround.x;
                moveAlongGround.x *= multiplier;
                moveAlongGround.y *= multiplier;
            }
            else
            {
                moveAlongGround *= downslopeVelMultiplier;
            }
        }
    }

    void SetInfo(Vector2 normal)
    {
        if (normal != Vector2.zero)
        {
            info.prevSlopeAngle = info.slopeAngle;
            info.slopeAngle = Vector2.Angle(normal, Vector2.up);

            if (info.slopeAngle <= maxClimbableSlopeAngle)
            {
                info.grounded = true; velocity.y = -0.1f;
                info.groundNormal = normal;
                info.prevGroundNormalPerp = info.groundNormalPerp;
                info.groundNormalPerp = -Vector2.Perpendicular(normal);
                info.unclimbableGroundNormal = Vector2.zero;
            }

            else
            {
                info.grounded = false;
                info.groundNormal = Vector2.up;
                info.prevGroundNormalPerp = info.groundNormalPerp;
                info.groundNormalPerp = Vector2.right;
                info.unclimbableGroundNormal = normal;
            }

        }

        else
        {
            info.grounded = false;
            info.prevSlopeAngle = info.slopeAngle;
            info.slopeAngle = -1;
            info.groundNormal = Vector2.up;
            info.prevGroundNormalPerp = info.groundNormalPerp;
            info.groundNormalPerp = Vector2.right;
            info.unclimbableGroundNormal = Vector2.zero;

        }
    }

    public struct Info
    {
        public Vector2 groundNormal;
        public Vector2 prevGroundNormalPerp;
        public Vector2 groundNormalPerp;
        public Vector2 unclimbableGroundNormal;

        public float slopeAngle;
        public float prevSlopeAngle;

        public bool grounded;
        public bool movingIntoClimbable;
        public bool deliberateAirborne;

        public float xDistMoved;

    }


}
