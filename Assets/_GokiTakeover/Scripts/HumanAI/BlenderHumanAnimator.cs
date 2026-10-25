using UnityEngine;
using UnityEngine.AI;

namespace GokiTakeover
{
    // Walk cycle plus a full-body slipper slam: squat, lean, raise overhead, swing, sole flat on the
    // strike point, lift. The slipper pose is solved first; arms and legs follow with two-bone IK.
    [DefaultExecutionOrder(50)]
    public sealed class BlenderHumanAnimator : MonoBehaviour
    {
        [SerializeField] private HumanAIController human;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Transform pelvis, chest, head, leftThigh, rightThigh, leftShin, rightShin, leftArm, rightArm, leftForearm, rightForearm, weapon;
        [SerializeField] private Transform spine, neck, leftFoot, rightFoot, leftHand, rightHand;
        // Held slipper in Weapon-bone space: tread face centre (sole faces -X, toe along -Y).
        private static readonly Vector3 TreadCentre = new Vector3(-0.025f, -0.29f, 0);
        private const float SquatDrop = 0.85f, ImpactLean = 78f;
        private float phase, blend, lastAttackTime;
        private Vector3 pelvisRest, weaponOffset, footRestL, footRestR;
        private Transform[] posed;
        private Quaternion[] walkRotation;
        // Exterminator (Phase 5): the spray lance in the left hand runs on from the arm; nozzleLocal is its tip in
        // left-hand space. SprayAim (0..1) raises the arm so the lance points at SprayPoint.
        [SerializeField] private Vector3 nozzleLocal;
        public float SprayAim { get; set; }
        public Vector3 SprayPoint { get; set; }
        public float AimWeight => aim;
        public Vector3 NozzleWorld => leftHand.TransformPoint(nozzleLocal);
        public void ConfigureLance(Vector3 tipInLeftHand) { nozzleLocal = tipInLeftHand; }
        private float aim;
        // Stage 2 daily life (HouseholdLife): a pose held at an activity, blended in and out. Seated poses put the hips on
        // SeatPoint (the person stands beside the chair on the NavMesh) with the feet on the floor; the hands work at
        // HandPoint (the worktop, desk, table or bed); the head watches LookPoint. HeldProp (the newspaper, the game pad)
        // is shown between the hands while reading or playing. None: the walk pose only (every other scene).
        public enum LifePose{None,Sit,Read,Desk,Eat,Doze,Floor,Cook,Wash,Fold}
        public LifePose Pose{get;set;}
        public Vector3 SeatPoint{get;set;}
        public Vector3 HandPoint{get;set;}
        public Vector3 LookPoint{get;set;}
        public Transform HeldProp{get;set;}
        public LifePose ShownPose=>shownPose;
        public float PoseWeight=>poseWeight;
        public float HipHeight=>hipHeight;
        public Vector3 LeftHandWorld=>leftHand.position;
        public Vector3 RightHandWorld=>rightHand.position;
        public Vector3 PelvisWorld=>pelvis.position;
        private LifePose shownPose;private float poseWeight,hipHeight;private Transform shownProp;
        public Vector3 TreadCentreWorld => weapon.TransformPoint(TreadCentre);
        public Vector3 TreadNormalWorld => -weapon.right;
        public void Configure(HumanAIController ai, Transform[] bones, Transform originalWeapon)
        {
            human=ai;agent=ai.GetComponent<NavMeshAgent>();
            pelvis=Find(bones,"Pelvis");spine=Find(bones,"Spine");chest=Find(bones,"Chest");neck=Find(bones,"Neck");head=Find(bones,"Head");
            leftThigh=Find(bones,"Thigh_L");rightThigh=Find(bones,"Thigh_R");leftShin=Find(bones,"Shin_L");rightShin=Find(bones,"Shin_R");
            leftFoot=Find(bones,"Foot_L");rightFoot=Find(bones,"Foot_R");
            leftArm=Find(bones,"Arm_L");rightArm=Find(bones,"Arm_R");leftForearm=Find(bones,"Forearm_L");rightForearm=Find(bones,"Forearm_R");
            leftHand=Find(bones,"Hand_L");rightHand=Find(bones,"Hand_R");weapon=Find(bones,"Weapon");
        }
        public static Transform Find(Transform[] bones,string name)
        { foreach(var bone in bones) if(bone.name==name)return bone;throw new System.InvalidOperationException("Missing model bone: "+name); }
        private void Awake()
        {
            if(!pelvis)return;
            pelvisRest=pelvis.localPosition;weaponOffset=weapon.localPosition;
            footRestL=transform.InverseTransformPoint(leftFoot.position);footRestR=transform.InverseTransformPoint(rightFoot.position);
            posed=new[]{spine,chest,neck,head,leftThigh,rightThigh,leftShin,rightShin,leftFoot,rightFoot,leftArm,rightArm,leftForearm,rightForearm,leftHand,rightHand,weapon,pelvis};
            walkRotation=new Quaternion[posed.Length];
            hipHeight=human?pelvis.position.y-human.transform.position.y:2;
        }
        private void LateUpdate()
        {
            if(!human || posed==null || Time.deltaTime<=0)return;
            Walk();
            Life(Time.deltaTime);
            aim=Mathf.MoveTowards(aim,Mathf.Clamp01(SprayAim),Time.deltaTime*(SprayAim>aim?3.2f:2.2f));
            bool attacking=human.State==HumanAIController.HumanState.Attack;
            if(attacking)lastAttackTime=human.AttackTime;
            blend=Mathf.MoveTowards(blend,attacking?1:0,Time.deltaTime*(attacking?7:3));
            if(blend<=0){if(aim>0)Aim(aim);return;}
            for(int i=0;i<posed.Length;i++)walkRotation[i]=posed[i].localRotation;
            Vector3 walkPelvis=pelvis.localPosition;
            Strike(lastAttackTime);
            for(int i=0;i<posed.Length;i++)posed[i].localRotation=Quaternion.Slerp(walkRotation[i],posed[i].localRotation,blend);
            pelvis.localPosition=Vector3.Lerp(walkPelvis,pelvis.localPosition,blend);
            if(aim>0)Aim(aim);
        }
        // Lean back, look up, swing the straight left arm up so the lance points at SprayPoint.
        private void Aim(float w)
        {
            if(nozzleLocal.sqrMagnitude<1e-6f)return;
            // Up at the ceiling: lean back. Down at the floor (Stage 2: the vacuum, the bug net): bend over it instead.
            bool low=SprayPoint.y<chest.position.y-1f;
            chest.localRotation=Quaternion.Slerp(chest.localRotation,chest.localRotation*Quaternion.Euler(low?18:-14,0,0),w);
            spine.localRotation=Quaternion.Slerp(spine.localRotation,spine.localRotation*Quaternion.Euler(low?10:-6,0,0),w);
            Vector3 shoulder=leftArm.position, toTarget=SprayPoint-shoulder;
            if(toTarget.sqrMagnitude<1e-4f)return;
            float reach=Vector3.Distance(leftArm.position,leftForearm.position)+Vector3.Distance(leftForearm.position,leftHand.position);
            Vector3 hand=shoulder+toTarget.normalized*reach*.96f;
            TwoBone(leftArm,leftForearm,leftHand,Vector3.Lerp(leftHand.position,hand,w),-transform.right*.4f-transform.up*.6f);
            Vector3 lance=leftHand.TransformDirection(nozzleLocal.normalized), want=(SprayPoint-leftHand.position).normalized;
            leftHand.rotation=Quaternion.Slerp(leftHand.rotation,Quaternion.FromToRotation(lance,want)*leftHand.rotation,w);
            Vector3 look=SprayPoint-head.position;
            if(look.sqrMagnitude>1e-3f)head.rotation=Quaternion.Slerp(head.rotation,Quaternion.LookRotation(look,chest.up),.7f*w);
        }
        private void Life(float dt)
        {
            if(Pose!=shownPose){poseWeight=Mathf.MoveTowards(poseWeight,0,dt*3.2f);if(poseWeight<=0)shownPose=Pose;}
            else poseWeight=Mathf.MoveTowards(poseWeight,shownPose==LifePose.None?0:1,dt*2.2f);
            if(shownProp&&shownProp!=HeldProp)shownProp.gameObject.SetActive(false);
            shownProp=HeldProp;
            bool holding=poseWeight>.55f&&(shownPose==LifePose.Read||shownPose==LifePose.Floor);
            if(HeldProp&&HeldProp.gameObject.activeSelf!=holding)HeldProp.gameObject.SetActive(holding);
            if(poseWeight<=0||shownPose==LifePose.None)return;
            for(int i=0;i<posed.Length;i++)walkRotation[i]=posed[i].localRotation;
            Vector3 walkPelvis=pelvis.localPosition;
            Hold(shownPose,Time.time);
            float w=Smooth(0,1,poseWeight);
            for(int i=0;i<posed.Length;i++)posed[i].localRotation=Quaternion.Slerp(walkRotation[i],posed[i].localRotation,w);
            pelvis.localPosition=Vector3.Lerp(walkPelvis,pelvis.localPosition,w);
            if(holding&&HeldProp)HeldProp.SetPositionAndRotation((leftHand.position+rightHand.position)*.5f+human.transform.forward*.05f,Quaternion.LookRotation(human.transform.forward,Vector3.up));
        }
        // The activity pose at full weight, solved in world space from the person's facing.
        private void Hold(LifePose p,float t)
        {
            Transform body=human.transform;Vector3 up=Vector3.up,fw=body.forward,side=body.right;
            float thigh=Vector3.Distance(leftThigh.position,leftShin.position),shin=Vector3.Distance(leftShin.position,leftFoot.position);
            float k=(thigh+shin)/1.9f, ankle=Mathf.Max(.04f,transform.TransformPoint(footRestL).y-body.position.y);
            bool seated=p==LifePose.Sit||p==LifePose.Read||p==LifePose.Desk||p==LifePose.Eat||p==LifePose.Doze||p==LifePose.Floor;
            Vector3 hips=pelvis.position;
            if(seated){hips=SeatPoint+up*(thigh+shin)*.07f;pelvis.position=hips;}
            float lean=p==LifePose.Desk?18:p==LifePose.Eat?8:p==LifePose.Doze?-10:p==LifePose.Sit?-5:p==LifePose.Floor?8:p==LifePose.Cook?10:p==LifePose.Wash?16:p==LifePose.Fold?30:0;
            spine.rotation=Quaternion.AngleAxis(lean*.5f,side)*spine.rotation;
            chest.rotation=Quaternion.AngleAxis(lean*.5f+(p==LifePose.Doze?Mathf.Sin(t*1.4f)*1.5f:0),side)*chest.rotation;
            // Legs: knees forward and feet on the floor in front of the seat; cross-legged on the floor.
            if(seated)
                for(int s=-1;s<=1;s+=2)
                {
                    Transform th=s<0?leftThigh:rightThigh,sh=s<0?leftShin:rightShin,ft=s<0?leftFoot:rightFoot;
                    Vector3 foot,pole;
                    if(p==LifePose.Floor){foot=hips+fw*thigh*.55f-side*s*thigh*.35f;pole=fw*.4f+side*s+up*.8f;}
                    else
                    {
                        // Knees over the front edge; on a seat too high for the legs (the sofa) the thighs lie along it.
                        float reach=p==LifePose.Doze?thigh*1.05f+shin*.35f:hips.y-(thigh+shin)*.92f>body.position.y+ankle?thigh+shin*.25f:thigh*.92f;
                        foot=hips+fw*reach+side*s*.24f*k;pole=fw+up*.3f;
                    }
                    foot.y=Mathf.Max(body.position.y+ankle,hips.y-(thigh+shin)*.92f);   // the feet dangle from a seat too high for the legs (the sofa)
                    TwoBone(th,sh,ft,foot,pole);
                    ft.rotation=body.rotation;
                }
            // Arms: on the lap, holding the paper or the pad, working on the surface in front.
            Vector3 lh,rh;
            switch(p)
            {
                case LifePose.Read:{Vector3 c=chest.position+fw*.55f*k+up*.02f*k;lh=c-side*.3f*k;rh=c+side*.3f*k;break;}
                case LifePose.Desk:lh=HandPoint-side*.28f*k;rh=HandPoint+side*.18f*k+(fw*Mathf.Sin(t*5)+side*Mathf.Cos(t*5))*.05f*k;break;
                case LifePose.Eat:{float bite=Mathf.Clamp01(Mathf.Sin(t*2.1f)*2-.6f);lh=HandPoint-side*.3f*k;rh=Vector3.Lerp(HandPoint+side*.25f*k,head.position+fw*.22f*k-up*.12f*k,bite);break;}
                case LifePose.Floor:{Vector3 pad=chest.position+fw*.42f*k-up*.38f*k;lh=pad-side*.1f*k+up*Mathf.Sin(t*13)*.012f*k;rh=pad+side*.1f*k+up*Mathf.Sin(t*11+1)*.012f*k;break;}
                case LifePose.Cook:lh=HandPoint-side*.42f*k;rh=HandPoint+side*.05f*k+(fw*Mathf.Sin(t*3.2f)+side*Mathf.Cos(t*3.2f))*.12f*k;break;
                case LifePose.Wash:{float scrub=Mathf.Sin(t*6)*.08f*k;lh=HandPoint-side*.2f*k+fw*scrub;rh=HandPoint+side*.2f*k-fw*scrub;break;}
                case LifePose.Fold:{float f=Mathf.Sin(t*1.7f);Vector3 lift=up*Mathf.Max(0,f)*.15f*k;lh=HandPoint-side*(.25f+.2f*f)*k+lift;rh=HandPoint+side*(.25f+.2f*f)*k+lift;break;}
                default:lh=Vector3.Lerp(hips,leftShin.position,.55f)+up*.12f*k;rh=Vector3.Lerp(hips,rightShin.position,.55f)+up*.12f*k;break;   // Sit, Doze: hands on the lap
            }
            TwoBone(leftArm,leftForearm,leftHand,lh,-up*.6f-fw*.2f-side*.5f);
            TwoBone(rightArm,rightForearm,rightHand,rh,-up*.6f-fw*.2f+side*.5f);
            // Head: nodding off, else watching the TV, the pan, the paper.
            if(p==LifePose.Doze){head.rotation=Quaternion.AngleAxis(32,side)*Quaternion.AngleAxis(16,fw)*head.rotation;return;}
            Vector3 look=(p==LifePose.Read||p==LifePose.Floor&&LookPoint.sqrMagnitude<1e-4f?(leftHand.position+rightHand.position)*.5f:LookPoint)-head.position;
            if(look.sqrMagnitude>1e-3f)head.rotation=Quaternion.Slerp(head.rotation,Quaternion.LookRotation(look,chest.up),.7f);
        }
        private void Walk()
        {
            float speed=agent.velocity.magnitude, move=Mathf.Clamp01(speed/.8f);
            phase+=Time.deltaTime*Mathf.Lerp(3.5f,6.8f,Mathf.Clamp01(speed/2));
            float gait=Mathf.Sin(phase)*23*move;
            pelvis.localPosition=pelvisRest+Vector3.up*(Mathf.Abs(Mathf.Sin(phase))*.018f*move);
            pelvis.localRotation=Quaternion.Euler(0,Mathf.Sin(phase)*3*move,0);
            spine.localRotation=Quaternion.Euler(1.5f*move,-Mathf.Sin(phase)*2*move,0);
            leftThigh.localRotation=Quaternion.Euler(gait,0,0);rightThigh.localRotation=Quaternion.Euler(-gait,0,0);
            leftShin.localRotation=Quaternion.Euler(Mathf.Max(0,-Mathf.Sin(phase))*28*move,0,0);
            rightShin.localRotation=Quaternion.Euler(Mathf.Max(0,Mathf.Sin(phase))*28*move,0,0);
            leftFoot.localRotation=Quaternion.Euler(-Mathf.Max(0,-Mathf.Sin(phase))*12*move,0,0);
            rightFoot.localRotation=Quaternion.Euler(-Mathf.Max(0,Mathf.Sin(phase))*12*move,0,0);
            chest.localRotation=Quaternion.Euler(3*move,0,Mathf.Sin(phase)*1.3f*move);
            neck.localRotation=Quaternion.identity;
            head.localRotation=Quaternion.Euler(human.State==HumanAIController.HumanState.Routine?10:23,Mathf.Sin(Time.time*.6f)*3,0);
            leftArm.localRotation=Quaternion.Euler(-gait*.55f,0,3);
            rightArm.localRotation=Quaternion.Euler(gait*.45f,0,-3);
            leftForearm.localRotation=Quaternion.Euler(-9-5*move,0,0);
            rightForearm.localRotation=Quaternion.Euler(-12,0,0);
            leftHand.localRotation=rightHand.localRotation=weapon.localRotation=Quaternion.identity;
        }
        private void Strike(float t)
        {
            var s=human.Settings;float windup=s.attackWindup,recover=s.attackRecover;
            // Phase 5 reach: the sole may meet the worktop (a higher target, less squat) or a wall (sole turned to it).
            Vector3 face=human.StrikeUp, toe=human.StrikeAlong;
            Vector3 floorTarget=human.AttackPoint;
            if(face.y<.99f)floorTarget+=face*(human.StrikeSurfaceHeight-.025f);
            else floorTarget.y=transform.position.y+human.StrikeSurfaceHeight;
            float height=floorTarget.y-transform.position.y, low=height<.5f?1:Mathf.Clamp01(1-(height-.5f)/2.2f);
            float crouch=(t<windup?Smooth(0,windup*.7f,t):Mathf.Lerp(1,.25f,Smooth(windup+.12f,windup+recover,t)))*low;
            float raise=Smooth(windup*.05f,windup*.6f,t);
            float swing=t<windup*.62f?0:Mathf.Pow(Mathf.Clamp01((t-windup*.62f)/(windup*.38f)),2);
            float lift=Smooth(windup+.12f,windup+recover,t);
            float lean=t<windup*.62f?25*crouch:Mathf.Lerp(25,ImpactLean,swing);
            if(t>=windup)lean=Mathf.Lerp(ImpactLean,15,lift);
            float twist=t<windup*.62f?-12*raise:Mathf.Lerp(-12,10,swing);
            Vector3 up=transform.up, forward=human.StrikeForward, side=Vector3.Cross(up,forward);

            // Hanging slipper pose (from the walk pose) before the body bends.
            Quaternion hangRotation=weapon.rotation;Vector3 hangOffset=weapon.position-rightArm.position;
            pelvis.localPosition=pelvisRest+new Vector3(0,-SquatDrop*crouch,-.16f*crouch);
            pelvis.localRotation=Quaternion.Euler(lean*.22f,0,0);
            spine.localRotation=Quaternion.Euler(lean*.36f,twist*.4f,0);
            chest.localRotation=Quaternion.Euler(lean*.42f,twist*.6f,0);

            Quaternion impactRotation=face.y<.99f?Axes(face,-toe):Axes(up,-forward);
            Vector3 impactWeapon=floorTarget+(face.y<.99f?face:up)*.004f-impactRotation*TreadCentre;
            Vector3 toeUp=(up*Mathf.Cos(25*Mathf.Deg2Rad)-forward*Mathf.Sin(25*Mathf.Deg2Rad)).normalized;
            Vector3 treadForward=(forward*Mathf.Cos(25*Mathf.Deg2Rad)+up*Mathf.Sin(25*Mathf.Deg2Rad)).normalized;
            Quaternion raisedRotation=Axes(-treadForward,-toeUp);
            float reach=Vector3.Distance(rightArm.position,rightForearm.position)+Vector3.Distance(rightForearm.position,rightHand.position);
            // Lunge the hips toward the strike when the squat alone cannot reach the floor.
            if(swing>0&&t<windup+recover)
            {
                Vector3 wrist=impactWeapon-impactRotation*weaponOffset;
                Vector3 gap=wrist-rightArm.position;float excess=gap.magnitude-reach*.97f;
                if(excess>0)pelvis.position+=gap.normalized*excess*(t<windup?swing:1-lift);
            }
            Vector3 shoulder=rightArm.position;
            Vector3 raisedOffset=up*.62f-forward*.3f+side*.12f;
            Vector3 offset;Quaternion rotation;
            if(t<windup*.62f){offset=Vector3.Slerp(hangOffset,raisedOffset,raise);rotation=Quaternion.Slerp(hangRotation,raisedRotation,raise);}
            else if(t<windup){offset=Vector3.Slerp(raisedOffset,impactWeapon-shoulder,swing);rotation=Quaternion.Slerp(raisedRotation,impactRotation,swing);}
            else{offset=Vector3.Slerp(impactWeapon-shoulder,hangOffset,lift);rotation=Quaternion.Slerp(impactRotation,hangRotation,lift);}
            Vector3 weaponTarget=shoulder+offset;

            // Feet stay planted; the slipper-side foot steps toward the strike as the hips drop.
            Vector3 footL=transform.TransformPoint(footRestL+new Vector3(-.05f,0,-.12f)*crouch);
            Vector3 footR=transform.TransformPoint(footRestR+new Vector3(.06f,0,.3f)*crouch);
            TwoBone(leftThigh,leftShin,leftFoot,footL,forward-side*.3f);
            TwoBone(rightThigh,rightShin,rightFoot,footR,forward+side*.3f);
            leftFoot.rotation=rightFoot.rotation=transform.rotation;

            TwoBone(rightArm,rightForearm,rightHand,weaponTarget-rotation*weaponOffset,side*.8f+up*.2f-forward*.3f);
            rightHand.rotation=rotation;weapon.localRotation=Quaternion.identity;
            Vector3 knee=leftShin.position;
            TwoBone(leftArm,leftForearm,leftHand,Vector3.Lerp(leftHand.position,knee+forward*.12f+up*.1f,crouch),-side*.6f-forward*.4f);

            Vector3 look=floorTarget-head.position;
            if(look.sqrMagnitude>1e-3f)head.rotation=Quaternion.Slerp(head.rotation,Quaternion.LookRotation(look,chest.up),.65f*crouch);
        }
        private static Quaternion Axes(Vector3 x,Vector3 y)=>Quaternion.LookRotation(Vector3.Cross(x,y),y);
        private static float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        // Places end at target (clamped to reach), bending the middle joint toward pole.
        private static void TwoBone(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            Vector3 a=upper.position;float l1=Vector3.Distance(a,lower.position),l2=Vector3.Distance(lower.position,end.position);
            Vector3 toTarget=target-a;float d=Mathf.Clamp(toTarget.magnitude,Mathf.Abs(l1-l2)+.001f,l1+l2-.001f);
            Vector3 direction=toTarget.sqrMagnitude>1e-8f?toTarget.normalized:upper.up*-1;
            Vector3 bend=Vector3.ProjectOnPlane(pole,direction);
            if(bend.sqrMagnitude<1e-6f)bend=Vector3.ProjectOnPlane(upper.forward,direction);
            bend.Normalize();
            float cos=Mathf.Clamp((l1*l1+d*d-l2*l2)/(2*l1*d),-1,1);
            Vector3 joint=a+direction*(l1*cos)+bend*(l1*Mathf.Sqrt(1-cos*cos));
            upper.rotation=Quaternion.FromToRotation(lower.position-a,joint-a)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,a+direction*d-lower.position)*lower.rotation;
        }
    }
}
