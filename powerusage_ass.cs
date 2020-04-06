// power usage and assembler queue display script.
// edit the name of the LCD per the LCD_name variable.
// code by vrav, 3/2020

IMyTextSurface lcd;

readonly string LCD_name = "LCD (Power/Production)";
readonly float block_refresh_time = 10f; // seconds
readonly UpdateFrequency update_frequency = UpdateFrequency.Update100;

IDictionary<string, int> combined_queue = new Dictionary<string, int>();
IDictionary<string, int> scratch_queue = new Dictionary<string, int>();

List<IMyAssembler> assemblers = new List<IMyAssembler>();

private System.DateTime prev_time;
float delta = 0f;

List<IMyPowerProducer> pbs_solar = new List<IMyPowerProducer>();
List<IMyPowerProducer> pbs_wind = new List<IMyPowerProducer>();
List<IMyPowerProducer> pbs_battery = new List<IMyPowerProducer>();
List<IMyPowerProducer> pbs_reactor = new List<IMyPowerProducer>();

public void GetAssemblers() {
    List<IMyTerminalBlock> scratch = new List<IMyTerminalBlock>();

    GridTerminalSystem.GetBlocksOfType<IMyAssembler>(scratch);
    assemblers.Clear();
    assemblers.AddRange( scratch.ConvertAll( x => (IMyAssembler)x ) );
}

public string GetAssemblerProductionString() {
    string str = "";

    str += "Currently producing:\n";
    combined_queue.Clear();

    foreach( IMyAssembler assembler in assemblers ) {
        List<MyProductionItem> raw_queue = new List<MyProductionItem>();
        assembler.GetQueue(raw_queue);

        if( raw_queue.Count == 0 ) {
            continue;
        }

        scratch_queue.Clear();
        foreach( MyProductionItem item in raw_queue ) {
            string name = item.BlueprintId.SubtypeName;
            int amount = (int)item.Amount;
            if( !scratch_queue.ContainsKey(name) ) {
                scratch_queue[name] = amount;
            } else {
                scratch_queue[name] += amount;
            }
        }

        foreach( string name in scratch_queue.Keys ) {
            if( !combined_queue.ContainsKey(name) ) {
                combined_queue[name] = scratch_queue[name];
            } else {
                combined_queue[name] += scratch_queue[name];
            }
        }
    }
    
    if( combined_queue.Count == 0 ) {
        str += "  Nada.\n";
    } else {
        foreach( string name in combined_queue.Keys ) {
            int amount = combined_queue[name];
            str += $"  {amount} {name}\n";
        }
    }

    return str;
}

public void SetPowerProducers( List<IMyTerminalBlock> scratch, List<IMyPowerProducer> pbs ) {
    pbs.Clear();
    pbs.AddRange( scratch.ConvertAll( x => (IMyPowerProducer)x ) );
}

public void GetPowerProducers() {
    List<IMyTerminalBlock> scratch = new List<IMyTerminalBlock>();

    GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(scratch);
    SetPowerProducers( scratch, pbs_solar );
    scratch.Clear();

    GridTerminalSystem.GetBlocksOfType<IMyPowerProducer>(scratch);
    scratch = (List<IMyTerminalBlock>)scratch.Where( x => x.BlockDefinition.SubtypeName.EndsWith("Turbine") ).ToList();
    SetPowerProducers( scratch, pbs_wind );
    scratch.Clear();

    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(scratch);
    SetPowerProducers( scratch, pbs_battery );
    scratch.Clear();

    GridTerminalSystem.GetBlocksOfType<IMyReactor>(scratch);
    SetPowerProducers( scratch, pbs_reactor );
    
    prev_time = System.DateTime.UtcNow;
}

public string GetPowerUsageStatsLine( string name, List<IMyPowerProducer> pbs ) {
    float total_current_output = 0.0f;
    float total_max_output = 0.0001f;
    int percentage = 0;

    foreach( IMyPowerProducer pb in pbs ) {
        total_current_output += pb.CurrentOutput;
        total_max_output += pb.MaxOutput;
    }

    percentage = (int)(( total_current_output / total_max_output ) * 100.0f);

    string tco = total_current_output.ToString("n2");
    string tmo = total_max_output.ToString("n2");

    if( percentage == 0 ) {
        if( total_max_output == 0.0001f ) {
            return "";
        } else {
            return $"{name}: 0.00/{tmo}MW (0%)\n";
        }
    } else {
        return $"{name}: {tco}/{tmo}MW ({percentage}%)\n";
    }
}

public string GetPowerUsageString() {
    string str = "Power usage:\n";
    str += GetPowerUsageStatsLine( "  S", pbs_solar );
    str += GetPowerUsageStatsLine( "  W", pbs_wind );
    str += GetPowerUsageStatsLine( "  B", pbs_battery );
    str += GetPowerUsageStatsLine( "  R", pbs_reactor );
    
    return str;
}

public void UpdateBlockLists() {
    GetPowerProducers();
    GetAssemblers();
}

public Program() {
    Runtime.UpdateFrequency = update_frequency;

    lcd = GridTerminalSystem.GetBlockWithName(LCD_name) as IMyTextSurface;
    UpdateBlockLists();
}

public void Main( string argument, UpdateType updateSource ) {
    string str = $"{GetPowerUsageString()}\n{GetAssemblerProductionString()}\n";
    lcd.WriteText(str, false);

    System.DateTime now = System.DateTime.UtcNow;
    delta += (float)(now - prev_time).TotalSeconds;
    // string update_seconds = (block_refresh_time - delta).ToString("n2");
    // lcd.WriteText($"Updating block lists in:\n  {update_seconds} seconds", true);
    if( delta > block_refresh_time ) {
        UpdateBlockLists();
        delta = 0f;
    }
    prev_time = System.DateTime.UtcNow;
}
