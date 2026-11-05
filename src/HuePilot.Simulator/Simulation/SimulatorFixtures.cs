namespace HuePilot.Simulator.Simulation;

public static class SimulatorFixtures
{
    public static SimulatorSnapshot Create()
    {
        var kitchenCeiling = new SimulatedLight(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Kitchen ceiling",
            true, 70, new HueColor(0.38, 0.38));
        var kitchenCounter = new SimulatedLight(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Kitchen counter",
            true, 55, new HueColor(0.45, 0.41));
        var loungeFloor = new SimulatedLight(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Lounge floor lamp",
            false, 40, new HueColor(0.31, 0.33));
        var officeDesk = new SimulatedLight(Guid.Parse("44444444-4444-4444-4444-444444444444"), "Office desk lamp",
            true, 60, new HueColor(0.34, 0.36));

        var kitchen = new SimulatedGroup(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Kitchen",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"), [kitchenCeiling.Id, kitchenCounter.Id]);
        var lounge = new SimulatedGroup(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Lounge",
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01"), [loungeFloor.Id]);
        var office = new SimulatedGroup(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Office",
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccc01"), [officeDesk.Id]);
        var downstairs = new SimulatedGroup(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "Downstairs",
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01"), [kitchenCeiling.Id, kitchenCounter.Id, loungeFloor.Id]);

        return new SimulatorSnapshot(
            [kitchenCeiling, kitchenCounter, loungeFloor, officeDesk],
            [kitchen, lounge, office],
            [downstairs],
            [],
            [new SimulatedScene(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Focus", [officeDesk.Id], 85,
                new HueColor(0.31, 0.32))]);
    }
}
