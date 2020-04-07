public enum RotorState {
    None,
    Retracted,
    Extended,
}

public enum TurretState {
    None,
    RandomizeAngles,
    WaitUntilAnglesMet,
    RandomizePower,
    WaitForReload,
    Retract,
    Extend,
    Fire,
    BeginWait,
    Wait,
}

public struct DoubleRange {
    public DoubleRange(double minimum, double maximum) {
        this.min = minimum;
        this.max = maximum;
    }
    public double min;
    public double max;
}

public Turret turret;
public static DoubleRange power_range = new DoubleRange( -0.11, -0.07f );
public static DoubleRange pitch_range = new DoubleRange( -20.0, 20.0 );
public static DoubleRange yaw_range = new DoubleRange( -360.0, 360.0 );
public float rotate_speed = 3.5f;
public double wait_seconds = 2.0;

public static TurretState[] seq = new TurretState[] {
    TurretState.WaitForReload,
    TurretState.RandomizeAngles,
    TurretState.WaitUntilAnglesMet,
    TurretState.Retract,
    TurretState.Extend,
    TurretState.Fire,
    TurretState.BeginWait,
    TurretState.Wait,
};

public class Turret {
    public Turret( IMyMotorStator pitch_rotor, IMyMotorStator yaw_rotor,
                   IMyMotorStator launch_rotor, List<IMyMotorStator> extending_rotors,
                   TurretState[] state_seq, float speed, double wait_time ) {
        pitch = pitch_rotor;
        pitch_target_angle = 0f;

        yaw = yaw_rotor;
        yaw_target_angle = 0f;

        launcher = launch_rotor;
        extenders = extending_rotors;

        // rotor_state = RotorState.None;

        state_sequence = state_seq;

        state_step = 0;
        running = false;

        displace_min = (float)power_range.min;
        displace_max = (float)power_range.max;

        rot_speed = speed;
        wait = wait_time;
    }
    // https://stackoverflow.com/a/13290596
    private double NextDoubleRange(Random random, double minValue, double maxValue) {
        return random.NextDouble() * (maxValue - minValue) + minValue;
    }
    
    public void Start() {
        running = true;
    }

    public void Stop() {
        running = false;
    }

    public void Toggle() {
        running = !running;
    }

    private void RandomizeAngles() {
        Random random = new Random();
        double offs = 20.0;
        pitch_target_angle += (float)NextDoubleRange(random, -offs, offs);
        pitch_target_angle = Math.Min(Math.Max(pitch_target_angle, (float)pitch_range.min), (float)pitch_range.max);
        yaw_target_angle += (float)NextDoubleRange(random, -offs, offs);
        yaw_target_angle = Math.Min(Math.Max(yaw_target_angle, (float)yaw_range.min), (float)yaw_range.max);
    }

    private float GetRotorAngle( IMyMotorStator r ) {
        return r.Angle * (180f / (float)Math.PI);
    }

    private void TurnRotorTowardsAngle( IMyMotorStator r, float angle ) {
        float current_value = GetRotorAngle(r);

        if( angle < 0f ) {
            r.LowerLimitDeg = angle;
        } else if( angle > 0f ) {
            r.UpperLimitDeg = angle;
        } else { // zero
            if( current_value < 0f ) {
                r.UpperLimitDeg = 0f;
            } else {
                r.LowerLimitDeg = 0f;
            }
        }

        float cur_vel = r.TargetVelocityRad;
        float sign = angle < current_value ? -1f : 1f;

        float target_velocity = rot_speed * sign;
        r.TargetVelocityRad = MathHelper.Lerp(cur_vel, target_velocity, 0.01f);
    }

    private bool RotorIsNearAngle( IMyMotorStator r, float angle ) {
        float current_value = GetRotorAngle(r);

        float variance = 1f;
        if( current_value > angle - variance && current_value < angle + variance ) {
            return true;
        }
        return false;
    }
    
    private void Retract() {
        foreach( IMyMotorStator rotor in extenders ) {
            rotor.Displacement = displace_min;
        }
    }

    private void Extend() {
        foreach( IMyMotorStator rotor in extenders ) {
            rotor.Displacement = displace_max;
        }
    }

    private void Fire() {
        launcher.Detach();
    }

    private bool Wait() {
        return (System.DateTime.UtcNow - wait_start).TotalSeconds > wait;
    }

    public void IncreaseStep() {
        state_step = (state_step + 1) % state_sequence.Length;
    }

    public void Update() {
        if( !running ) {
            return;
        }

        switch( state_sequence[state_step] ) {
            case TurretState.RandomizeAngles:
                RandomizeAngles();
                IncreaseStep();
                break;
            case TurretState.WaitUntilAnglesMet:
                bool all_good = true;

                if( RotorIsNearAngle(pitch, pitch_target_angle) ) {
                    pitch.TargetVelocityRad = 0f;
                } else {
                    TurnRotorTowardsAngle(pitch, pitch_target_angle);
                    all_good = false;
                }

                if( RotorIsNearAngle(yaw, yaw_target_angle) ) {
                    yaw.TargetVelocityRad = 0f;
                } else {
                    TurnRotorTowardsAngle(yaw, yaw_target_angle);
                    all_good = false;
                }
                                
                if( all_good ) {
                    IncreaseStep();
                }
                break;
            case TurretState.WaitForReload:
                launcher.ApplyAction("Add Top Part");
                IncreaseStep();
                break;
            case TurretState.Retract:
                Retract();
                IncreaseStep();
                break;
            case TurretState.Extend:
                Extend();
                IncreaseStep();
                break;
            case TurretState.Fire:
                Fire();
                IncreaseStep();
                break;
            case TurretState.BeginWait:
                wait_start = System.DateTime.UtcNow;
                IncreaseStep();
                break;
            case TurretState.Wait:
                if( Wait() ) {
                    IncreaseStep();
                };
                break;
            default:
                break;
        }
    }

    private IMyMotorStator pitch;
    public float pitch_target_angle { get; set; }

    private IMyMotorStator yaw;
    public float yaw_target_angle { get; set; }

    public float rot_speed { get; set; }

    private IMyMotorStator launcher;
    private List<IMyMotorStator> extenders;

    // private RotorState rotor_state;
    public TurretState[] state_sequence;
    public int state_step { get; set; }

    public bool running { get; set; }

    private float displace_min;
    public float displace_max { get; set; }

    private System.DateTime wait_start;
    private double wait;
}

public List<IMyMotorStator> extending_rotors = new List<IMyMotorStator>();
public IMyMotorStator launch_rotor;

public IMyMotorStator pitch_rotor;
public IMyMotorStator yaw_rotor;

public float displace_min = -.11f;
public float displace_max = .065f;

public IMyTextSurface lcd;
public IMyTextSurface lcd2;

public Program() {
    lcd = Me.GetSurface(0);
    lcd2 = Me.GetSurface(1);

    List<IMyTerminalBlock> scratch = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(scratch);
    extending_rotors.AddRange( scratch.ConvertAll( x => (IMyMotorStator)x ) );
    extending_rotors.RemoveAll(x => (x.CustomName == "Rotor (Pitch)" || x.CustomName == "Rotor (Yaw)"));

    launch_rotor = GridTerminalSystem.GetBlockWithName("Rotor (Launch)") as IMyMotorStator;
    pitch_rotor = GridTerminalSystem.GetBlockWithName("Rotor (Pitch)") as IMyMotorStator;
    yaw_rotor = GridTerminalSystem.GetBlockWithName("Rotor (Yaw)") as IMyMotorStator;

    turret = new Turret( pitch_rotor, yaw_rotor, launch_rotor, extending_rotors,
                         seq, rotate_speed, wait_seconds );
    
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

public void Main(string argument, UpdateType updateSource) {
    switch( argument ) {
        case "toggle":
            turret.Toggle();
            break;
        case "advance":
            turret.IncreaseStep();
            break;
        default:
            break;
    }

    turret.Update();
    lcd.WriteText(turret.running.ToString() + "\n");
    lcd.WriteText(turret.state_sequence[turret.state_step].ToString() + "\n", true);
    lcd.WriteText(turret.pitch_target_angle.ToString("n2") + "\n", true);
    lcd.WriteText(turret.yaw_target_angle.ToString("n2") + "\n", true);
}
