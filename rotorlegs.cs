// animation system for moving robot legs.
// part names and types defined at the top.
// animations are defined in the Program function.
// code by vrav, 4/2020

public enum PartType {
    Rotor,
    Piston,
    LandingGear,
}

public static IDictionary<string, PartType> parts = new Dictionary<string, PartType> {
    {"Rotor Hip Right", PartType.Rotor},
    {"Rotor Ankle Right", PartType.Rotor},
    {"Rotor Hip Left", PartType.Rotor},
    {"Rotor Ankle Left", PartType.Rotor},
    {"Piston Leg Right", PartType.Piston},
    {"Piston Leg Left", PartType.Piston},
    {"Landing Gear Foot Right", PartType.LandingGear},
    {"Landing Gear Foot Left", PartType.LandingGear},
};

IMyTextSurface lcd;

public class RotorPart {
    public RotorPart(IMyMotorStator p, float tv) {
        part = p;
        target_velocity = tv;
    }

    public IMyMotorStator part;
    public float target_velocity;
}

public class PistonPart {
    public PistonPart(IMyPistonBase p, float tv) {
        part = p;
        target_velocity = tv;
    }

    public IMyPistonBase part;
    public float target_velocity;
}

public class LandingGearPart {
    public LandingGearPart(IMyLandingGear p) {
        part = p;
        just_toggled = false;
        last_attempted = System.DateTime.UtcNow;
    }

    public IMyLandingGear part;
    public bool just_toggled;
    public System.DateTime last_attempted;
}

public class Robot {
    public Robot(float spd) {
        rotors = new Dictionary<string, RotorPart>();
        pistons = new Dictionary<string, PistonPart>();
        landing_gear = new Dictionary<string, LandingGearPart>();
        move_speed = spd;
    }

    public void AddParts( IDictionary<string, PartType> p, IMyGridTerminalSystem gts ) {
        foreach( string part_name in p.Keys ) {
            PartType part_type = p[part_name];
            if( part_type == PartType.Rotor ) {
                rotors[part_name] = new RotorPart( gts.GetBlockWithName(part_name) as IMyMotorStator, 0f );
            } else if( part_type == PartType.Piston ) {
                pistons[part_name] = new PistonPart( gts.GetBlockWithName(part_name) as IMyPistonBase, 0f );
            } else if( part_type == PartType.LandingGear ) {
                landing_gear[part_name] = new LandingGearPart( gts.GetBlockWithName(part_name) as IMyLandingGear );
            }
        }
    }
    
    public float GetRotorAngle( RotorPart rotor ) {
        return rotor.part.Angle * (180f / (float)Math.PI);
    }

    public float GetPistonPosition( PistonPart piston ) {
        return piston.part.CurrentPosition;
    }

    public RotorPart GetRotorByName( string name ) {
        if( rotors.ContainsKey(name) ) {
            return rotors[name];
        }
        return null;
    }

    public PistonPart GetPistonByName( string name ) {
        if( pistons.ContainsKey(name) ) {
            return pistons[name];
        }
        return null;
    }

    public LandingGearPart GetLandingGearByName( string name ) {
        if( landing_gear.ContainsKey(name) ) {
            return landing_gear[name];
        }
        return null;
    }

    public void TryToSetLandingGearToValue( string name, float value ) {
        LandingGearPart lg = GetLandingGearByName(name);
        if( lg == null ) {
            return;
        }
        bool desired = value == 0f ? false : true;

        System.DateTime last = lg.last_attempted;
        double time_since_last = (System.DateTime.UtcNow - last).TotalSeconds;
        
        if( lg.part.IsLocked != desired && time_since_last > 0.1 ) {
            lg.part.ToggleLock();
            lg.last_attempted = System.DateTime.UtcNow;
            if( !lg.part.IsLocked ) {
                foreach( string lg_name in landing_gear.Keys ) {
                    LandingGearPart lg_part = landing_gear[lg_name];
                    if( lg_name != name && lg_part.part.IsLocked ) {
                        lg_part.part.ToggleLock();
                        lg_part.last_attempted = System.DateTime.UtcNow;
                    }
                }
            }
        }
    }

    public bool IsLandingGearAtValue( string name, float value ) {
        LandingGearPart lg = GetLandingGearByName(name);
        if( lg == null ) {
            return true;
        }
        bool desired = value == 0f ? false : true;

        return lg.part.IsLocked == desired;
    }

    public void TryToSetRotorToValue( string name, float value ) {
        RotorPart r = GetRotorByName(name);
        if( r == null ) {
            return;
        }
        float current_value = GetRotorAngle(r);

        if( value < 0f ) {
            r.part.LowerLimitDeg = value;
        } else if( value > 0f ) {
            r.part.UpperLimitDeg = value;
        } else { // zero
            if( current_value < 0f ) {
                r.part.UpperLimitDeg = 0f;
            } else {
                r.part.LowerLimitDeg = 0f;
            }
        }

        float cur_vel = r.part.TargetVelocityRad;
        float sign = value < current_value ? -1f : 1f;

        r.target_velocity = move_speed * sign;
        r.part.TargetVelocityRad = MathHelper.Lerp(cur_vel, r.target_velocity, 0.01f);
    }

    public bool IsRotorAtValue( string name, float value ) {
        RotorPart r = GetRotorByName(name);
        if( r == null ) {
            return true;
        }
        float current_value = GetRotorAngle(r);

        float variance = 1f;
        if( current_value > value - variance && current_value < value + variance ) {
            return true;
        }
        return false;
    }

    public void TryToSetPistonToValue( string name, float value ) {
        PistonPart p = GetPistonByName(name);
        if( p == null ) {
            return;
        }
        float current_value = GetPistonPosition(p);
        float diff = Math.Abs(current_value - value);

        p.part.MaxLimit = value;

        float cur_vel = p.part.Velocity;
        float sign = value < current_value ? -1f : 1f;
        p.target_velocity = move_speed * sign;

        if( value < current_value ) {
            p.part.Velocity = MathHelper.Lerp(cur_vel, p.target_velocity, 0.05f);
        } else {
            p.part.Velocity = MathHelper.Lerp(cur_vel, p.target_velocity, 0.05f);
        }
    }

    public bool IsPistonAtValue( string name, float value ) {
        PistonPart p = GetPistonByName(name);
        if( p == null ) {
            return true;
        }
        float current_value = GetPistonPosition(p);

        if( Math.Abs(value - current_value) < 0.05f ) {
            p.part.Velocity = 0f;
            return true;
        }
        return false;
    }

    public IDictionary<string, RotorPart> rotors;
    public IDictionary<string, PistonPart> pistons;
    public IDictionary<string, LandingGearPart> landing_gear;
    
    public float move_speed { get; set; }
}

public class Animation {
    public Animation(string n) {
        name = n;
        frames = new List<IDictionary<string, float>>();
    }

    public void AddFrame(IDictionary<string, float> frame ) {
        frames.Add(frame);
    }

    public string name { get; set; }
    public List<IDictionary<string, float>> frames { get; }
}

public Robot robolegs;
public Animation anim_walk = new Animation("walk");
public Animation anim_stand = new Animation("stand");
public Animation anim_still = new Animation("still");

public Animation current_anim;
public int current_frame = 0;

public Program() {
    robolegs = new Robot(1.0f);
    robolegs.AddParts(parts, GridTerminalSystem);

    lcd = Me.GetSurface(0);
    
    /*
        Standing still pose.
    */
    anim_still.AddFrame(
        new Dictionary<string, float> {
            { "Rotor Hip Right", 0f },
            { "Rotor Ankle Right", 0f },
            { "Piston Leg Right", 1.0f },
            { "Landing Gear Foot Right", 1f },

            { "Rotor Hip Left", 0f },
            { "Rotor Ankle Left", 0f },
            { "Piston Leg Left", 1.0f },
            { "Landing Gear Foot Left", 1f },
        }
    );

    /*
        Animated standing.
    */
    anim_stand.AddFrame(
        new Dictionary<string, float> {
            { "Rotor Hip Right", 0f },
            { "Rotor Ankle Right", 0f },
            { "Piston Leg Right", 0.8f },
            { "Landing Gear Foot Right", 1f },

            { "Rotor Hip Left", 0f },
            { "Rotor Ankle Left", 0f },
            { "Piston Leg Left", 1.0f },
            { "Landing Gear Foot Left", 1f },
        }
    );
    anim_stand.AddFrame(
        new Dictionary<string, float> {
            { "Rotor Hip Right", 0f },
            { "Rotor Ankle Right", 0f },
            { "Piston Leg Right", 1.0f },
            { "Landing Gear Foot Right", 1f },

            { "Rotor Hip Left", 0f },
            { "Rotor Ankle Left", 0f },
            { "Piston Leg Left", 0.8f },
            { "Landing Gear Foot Left", 1f },
        }
    );

    /*
        Simple walk animation.
    */
    
    float leg_angle = 45f;
    float leg_retract = 0.5f;

    anim_walk.AddFrame(
        new Dictionary<string, float> {
            { "Rotor Hip Right", -leg_angle },
            { "Rotor Ankle Right", leg_angle },
            { "Piston Leg Right", leg_retract },
            { "Landing Gear Foot Right", 0f },

            { "Rotor Hip Left", -leg_angle },
            { "Rotor Ankle Left", leg_angle },
            { "Piston Leg Left", 1.3f },
            { "Landing Gear Foot Left", 1f },
        }
    );
    anim_walk.AddFrame(
        new Dictionary<string, float> {
            { "Rotor Hip Right", 0f },
            { "Rotor Ankle Right", 0f },
            { "Piston Leg Right", 1.0f },
            { "Landing Gear Foot Right", 1f },

            { "Rotor Hip Left", 0f },
            { "Rotor Ankle Left", 0f },
            { "Piston Leg Left", leg_retract },
            { "Landing Gear Foot Left", 0f },
        }
    );
    anim_walk.AddFrame(
        new Dictionary<string, float> {
            { "Rotor Hip Right", leg_angle },
            { "Rotor Ankle Right", -leg_angle },
            { "Piston Leg Right", 1.3f },
            { "Landing Gear Foot Right", 1f },
            
            { "Rotor Hip Left", leg_angle },
            { "Rotor Ankle Left", -leg_angle },
            { "Piston Leg Left", leg_retract },
            { "Landing Gear Foot Left", 0f },
        }
    );
    anim_walk.AddFrame(
        new Dictionary<string, float> {
            { "Rotor Hip Right", 0f },
            { "Rotor Ankle Right", 0f },
            { "Piston Leg Right", leg_retract },
            { "Landing Gear Foot Right", 0f },
            
            { "Rotor Hip Left", 0f },
            { "Rotor Ankle Left", 0f },
            { "Piston Leg Left", 1.0f },
            { "Landing Gear Foot Left", 1f },
        }
    );

    current_anim = anim_still;

    lcd.WriteText($"frame 0/{current_anim.frames.Count - 1}\n", false);

    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

public void Main(string argument, UpdateType updateSource) {
    switch( argument ) {
        case "walk":
            current_anim = anim_walk;
            current_frame = 0;
            break;
        case "stand":
            current_anim = anim_stand;
            current_frame = 0;
            break;
        case "still":
            current_anim = anim_still;
            current_frame = 0;
            break;
        default:
            break;
    }

    lcd.WriteText(current_anim.name + "\n", false);
    lcd.WriteText($"frame {current_frame}/{current_anim.frames.Count - 1}", true);

    bool all_good = true;
    foreach( string part_name in current_anim.frames[current_frame].Keys ) {
        float val = current_anim.frames[current_frame][part_name];
        if( parts[part_name] == PartType.Rotor ) { // rotors
            if( robolegs.IsRotorAtValue( part_name, val ) ) {
                float next = current_anim.frames[(current_frame + 1) % (current_anim.frames.Count)][part_name];
                if( (val <= 0f && next > val) || (val >= 0f && next < val) ) {
                    // reset velocity if we're reversing direction next frame
                    robolegs.GetRotorByName(part_name).part.TargetVelocityRad = 0f;
                }
                continue;
            } else {
                robolegs.TryToSetRotorToValue( part_name, val );
                all_good = false;
            }
        } else if( parts[part_name] == PartType.Piston ) { // pistons
            if( robolegs.IsPistonAtValue( part_name, val ) ) {
                continue;
            } else {
                robolegs.TryToSetPistonToValue( part_name, val );
                all_good = false;
            }
        } else if( parts[part_name] == PartType.LandingGear ) { // landing gear
            if( robolegs.IsLandingGearAtValue( part_name, val ) ) {
                continue;
            } else {
                robolegs.TryToSetLandingGearToValue( part_name, val );
                all_good = false;
            }
        }
    }
    if( all_good ) {
        current_frame = (current_frame + 1) % (current_anim.frames.Count);
    }
}
