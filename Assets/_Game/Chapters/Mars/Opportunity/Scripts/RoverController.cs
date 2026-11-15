using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RoverController : MonoBehaviour
    {
        public MeshCollider eagleGround,valleyGround,travelGround,heatShieldGround;
        public Transform visual, tool, toolRotor;
        public RoverPresentationRig rig;
        public Transform[] wheels, steering;
        public ParticleSystem wheelDust;
        public TrailRenderer[] tracks;
        public LayerMask groundMask = 1 << 8;
        public float cruiseSpeed = 1.7f, fastSpeed = 2.65f, turnSpeed = 46;
        public bool Controllable { get; set; }
        public bool CinematicControl { get; set; }
        public bool DeploymentSupport {get;set;}
        public float FilmSpeed { get; private set; }
        public float Power { get; set; } = 1;
        public float Speed { get; private set; }
        public float DistanceDriven { get; private set; }
        CharacterController controller;
        Vector3 gravity;
        float[] wheelAngles;Vector3[] wheelPrevious;
        float previousYaw;bool wheelOdometry;
        Quaternion[] wheelRest, steeringRest;
        Vector3 previous;
        Vector3[] steeringRoot, wheelRestLocal;
        float[] lift;
        const float WheelRadius = .13f, FrontLever = .58f, BogieLever = .40f, MiddleLever = .25f, RearLever = .30f, MaxRocker = 20, MaxBogie = 25;
        Vector3[] trackAnchors;
        int clearTrackFrames;
        public Vector3 RegionCenter { get; set; }
        public float RegionRadius { get; set; } = 190;
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            wheelRest = new Quaternion[wheels.Length];
            steeringRest = new Quaternion[steering.Length];
            for (int i = 0; i < wheels.Length; i++) wheelRest[i] = wheels[i].localRotation;
            for (int i = 0; i < steering.Length; i++) steeringRest[i] = steering[i].localRotation;
            previous = transform.position;
            steeringRoot = new Vector3[steering.Length]; for (int i = 0; i < steering.Length; i++) steeringRoot[i] = transform.InverseTransformPoint(steering[i].position);
            wheelRestLocal = new Vector3[wheels.Length]; for (int i = 0; i < wheels.Length; i++) wheelRestLocal[i] = visual.InverseTransformPoint(wheels[i].position);
            lift = new float[wheels.Length];
            wheelAngles=new float[wheels.Length];wheelPrevious=new Vector3[wheels.Length];previousYaw=transform.eulerAngles.y;
            trackAnchors=new Vector3[tracks.Length];
            for(int i=0;i<tracks.Length;i++)if(tracks[i])trackAnchors[i]=transform.InverseTransformPoint(tracks[i].transform.position);
        }
        public float WheelRoll(int index)=>wheelAngles[index];
        public float WheelSteer(int index)=>Mathf.DeltaAngle(0,wheels[index].parent.localEulerAngles.y);
        public RoverPresentationRig Rig => rig;
        public static float SteeringFor(Vector3 axle,float forwardSpeed,float yawRadians)
        {
            float lateral=yawRadians*axle.z,longitudinal=forwardSpeed-yawRadians*axle.x;
            if(Mathf.Abs(lateral)+Mathf.Abs(longitudinal)<.001f)return 0;
            float angle=Mathf.Atan2(lateral,longitudinal)*Mathf.Rad2Deg;
            if(angle>90)angle-=180;if(angle< -90)angle+=180;
            return Mathf.Clamp(angle,-78,78);
        }
        void Update()
        {
            float dt = Time.deltaTime;
            var input = Controllable ? GameInput.Move : Vector2.zero;
            float limit = Mathf.Lerp(0, GameInput.Fast ? fastSpeed : cruiseSpeed, Mathf.InverseLerp(.03f, .3f, Power));
            Speed = CinematicControl ? FilmSpeed : Mathf.MoveTowards(Speed, input.y * limit, dt * .9f);
            float turn = input.x * turnSpeed * Mathf.Lerp(.45f, 1, Mathf.Abs(Speed) / fastSpeed) * Mathf.InverseLerp(.02f, .2f, Power);
            transform.Rotate(0, turn * dt, 0, Space.World);
            if (controller.enabled && !CinematicControl)
            {
                gravity.y = controller.isGrounded ? -.8f : Mathf.Max(-8, gravity.y - 3.71f * dt);
                var velocity = transform.forward * Speed + gravity;
                var offset = transform.position - RegionCenter;
                offset.y = 0;
                if (offset.magnitude > RegionRadius && Vector3.Dot(velocity, offset) > 0)
                    velocity -= Vector3.Project(velocity, offset);
                controller.Move(velocity * dt);
            }
            float distance = Vector3.Distance(transform.position, previous);
            DistanceDriven += distance;
            previous = transform.position;
            float omega=dt>0?Mathf.DeltaAngle(previousYaw,transform.eulerAngles.y)*Mathf.Deg2Rad/dt:0;previousYaw=transform.eulerAngles.y;
            for(int i=0;i<steering.Length;i++)
            {
                var axle=steering[i];float angle=SteeringFor(steeringRoot[i],Speed,omega);
                float current=Mathf.DeltaAngle(0,axle.localEulerAngles.y);
                axle.localRotation=steeringRest[i]*Quaternion.Euler(0,Mathf.MoveTowardsAngle(current,angle,dt*135),0);
            }
            FollowTerrain(dt);
            if (!DeploymentSupport&&GroundRay(transform.position + Vector3.up * 4,out var hit,12))
            {
                var localNormal = transform.InverseTransformDirection(hit.normal);
                var tilt = Quaternion.FromToRotation(Vector3.up, localNormal);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, tilt * Quaternion.Euler(Mathf.Sin(Time.time * 3.8f) * Mathf.Abs(Speed) * .3f, 0, 0), dt * 3);
            }
            for(int i=0;i<wheels.Length;i++)
            {
                var axle=wheels[i].parent;Vector3 p=axle.position,delta=p-wheelPrevious[i];delta.y=0;
                if(wheelOdometry&&delta.sqrMagnitude<4&&!(DeploymentSupport&&Mathf.Abs(FilmSpeed)<.01f))wheelAngles[i]+=Vector3.Dot(delta,axle.forward)/.13f*Mathf.Rad2Deg;
                wheels[i].localRotation=wheelRest[i]*Quaternion.AngleAxis(wheelAngles[i],Vector3.right);wheelPrevious[i]=p;
            }
            wheelOdometry=true;
            if (wheelDust != null) { var emission = wheelDust.emission; emission.rateOverTime = Mathf.Abs(Speed) * 12; }
            for(int i=0;i<tracks.Length;i++)
            {
                var track=tracks[i];if(!track)continue;
                // Always start from a fixed wheel footprint. A ray through the generic
                // ground layer can hit the rover/lander, lifting the trail every frame.
                Vector3 anchor=transform.TransformPoint(trackAnchors[i]);
                var terrain=TerrainFor(anchor);
                bool grounded=false;
                if(terrain&&terrain.Raycast(new Ray(anchor+Vector3.up*60,Vector3.down),out var footprint,120))
                {
                    track.transform.SetPositionAndRotation(footprint.point+footprint.normal*.012f,Quaternion.LookRotation(footprint.normal,transform.forward));
                    grounded=Mathf.Abs(anchor.y-footprint.point.y)<.65f;
                }
                if(clearTrackFrames>0){track.emitting=false;track.Clear();track.enabled=false;}
                else{track.enabled=true;track.emitting=grounded&&!DeploymentSupport&&Mathf.Abs(Speed)>.08f;}
            }
            if(clearTrackFrames>0)clearTrackFrames--;
        }
        // Rocker-bogie (RIG.md): alturas do terreno sob as seis rodas viram o diferencial e os dois bogies.
        void FollowTerrain(float dt)
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                Vector3 anchor = visual.TransformPoint(new Vector3(wheelRestLocal[i].x, 0, wheelRestLocal[i].z));
                if (GroundRay(anchor + Vector3.up * .6f, out var contact, 1.3f))
                {
                    float height = Mathf.Clamp(visual.InverseTransformPoint(contact.point).y, -.22f, .16f);
                    lift[i] = Mathf.Lerp(lift[i], height, 1 - Mathf.Exp(-dt * 12));
                }
            }
            SideAngles(0, out float rockerLeft, out float bogieLeft);
            SideAngles(3, out float rockerRight, out float bogieRight);
            rig.Suspend((rockerLeft - rockerRight) * .5f, bogieLeft, bogieRight);
        }

        // Alturas (frente, meio, trás) de um lado → rocker e bogie em graus, + = frente para baixo.
        void SideAngles(int first, out float rocker, out float bogie)
        {
            float front = lift[first], middle = lift[first + 1], rear = lift[first + 2];
            float bogieRad = (rear - middle) / (MiddleLever + RearLever);
            float fromFront = -front / FrontLever, fromBogie = (middle + MiddleLever * bogieRad) / BogieLever;
            rocker = Mathf.Clamp((fromFront + fromBogie) * .5f * Mathf.Rad2Deg, -MaxRocker, MaxRocker);
            bogie = Mathf.Clamp(bogieRad * Mathf.Rad2Deg, -MaxBogie, MaxBogie);
        }

        void ResetSuspension()
        {
            System.Array.Clear(lift, 0, lift.Length);
            rig.Suspend(0, 0, 0);
        }

        /// <summary>Distância (m) do centro do rover ao ponto de trabalho da RAT, ao longo do eixo frontal: ombro a 0,575 m + 0,73 m de braço. A essa distância o braço desdobra sem tocar a rocha e a ponta alcança o ponto com a broca na vertical. O ponto fica alinhado ao ombro, não ao centro.</summary>
        public const float WorkDistance = 1.305f;

        /// <summary>Deslocamento lateral (m, +X = direita) do ombro do braço IDD em relação ao centro do rover (RIG.md: 0,24).</summary>
        public const float ShoulderSide = .24f;

        /// <summary>Onde estacionar, com a orientação <paramref name="rotation"/>, para que <paramref name="workPoint"/> fique ao alcance do braço.</summary>
        public Vector3 WorkParking(Vector3 workPoint, Quaternion rotation)
        {
            Vector3 position = workPoint - rotation * (Vector3.forward * WorkDistance + Vector3.right * ShoulderSide);
            position.y = transform.position.y;
            return position;
        }

        public void Teleport(Vector3 position, float yaw)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            foreach(var track in tracks){track.emitting=false;track.enabled=false;track.Clear();}clearTrackFrames=3;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            Speed = 0; gravity = Vector3.zero; previous = position;
            wheelOdometry=false;previousYaw=yaw;
            controller.enabled = true;
            foreach (var track in tracks) track.Clear();
        }
        public bool GroundRay(Vector3 origin,out RaycastHit hit,float distance)
        {
            if(CinematicControl)
            {
                if(DeploymentSupport)
                {
                    // Look below the wheels, never down through the still-closed roof.
                    origin.y=Mathf.Min(origin.y,transform.position.y+.32f);
                    return Physics.Raycast(origin,Vector3.down,out hit,distance,groundMask);
                }
                var ground=transform.position.x>1900?heatShieldGround:transform.position.x< -500?travelGround:transform.position.x>500?valleyGround:eagleGround;
                if(ground)return ground.Raycast(new Ray(origin,Vector3.down),out hit,distance);
            }
            return Physics.Raycast(origin,Vector3.down,out hit,distance,groundMask);
        }
        public Vector3 TerrainPoint(Vector3 p)
        {
            var ground=TerrainFor(p);
            if(ground&&ground.Raycast(new Ray(p+Vector3.up*60,Vector3.down),out var hit,120))return new Vector3(p.x,hit.point.y+.025f,p.z);
            return p;
        }
        MeshCollider TerrainFor(Vector3 p)=>p.x>1900?heatShieldGround:p.x< -500?travelGround:p.x>500?valleyGround:eagleGround;
        public void Halt(){Speed=0;FilmSpeed=0;}
        public void ResetDistance() => DistanceDriven = 0;
        public void SetFilmPose(Vector3 position,float yaw,float speed)
        {if(controller.enabled)controller.enabled=false;
            if(Vector3.Distance(transform.position,position)>10){previous=position;wheelOdometry=false;previousYaw=yaw;ResetSuspension();foreach(var track in tracks){track.Clear();track.emitting=false;}clearTrackFrames=3;visual.localRotation=Quaternion.identity;}
            transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));FilmSpeed=speed;}
        public void ReleaseFilm(){DeploymentSupport=false;CinematicControl=false;FilmSpeed=0;if(controller!=null)controller.enabled=true;gravity=Vector3.zero;previous=transform.position;}
        public void AssistParking(Vector3 target,Quaternion rotation,float dt)
        {Vector3 delta=target-transform.position;delta.y=0;if(controller.enabled)controller.Move(Vector3.ClampMagnitude(delta,1)*dt*1.4f);transform.rotation=Quaternion.Slerp(transform.rotation,rotation,dt*3);}
    }
}
