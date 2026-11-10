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
        var bedroomCeiling = new SimulatedLight(Guid.Parse("55555555-5555-5555-5555-555555555555"), "Bedroom ceiling",
            false, 35, new HueColor(0.39, 0.37));
        var hallwayCeiling = new SimulatedLight(Guid.Parse("66666666-6666-6666-6666-666666666666"), "Hallway ceiling",
            true, 45, new HueColor(0.42, 0.4));
        var gardenPathLeft = new SimulatedLight(Guid.Parse("77777777-7777-7777-7777-777777777777"), "Garden path left",
            true, 50, new HueColor(0.5, 0.42));
        var gardenPathRight = new SimulatedLight(Guid.Parse("88888888-8888-8888-8888-888888888888"), "Garden path right",
            true, 50, new HueColor(0.5, 0.42));
        var gardenTerrace = new SimulatedLight(Guid.Parse("99999999-9999-9999-9999-999999999999"), "Garden terrace",
            false, 30, new HueColor(0.45, 0.4));
        var gardenSpotlight = new SimulatedLight(Guid.Parse("10101010-1010-1010-1010-101010101010"), "Garden spotlight",
            true, 75, new HueColor(0.32, 0.33));
        var gardenShed = new SimulatedLight(Guid.Parse("12121212-1212-1212-1212-121212121212"), "Garden shed",
            false, 20, new HueColor(0.47, 0.41));

        var kitchen = new SimulatedGroup(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Kitchen",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"), [kitchenCeiling.Id, kitchenCounter.Id]);
        var lounge = new SimulatedGroup(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Lounge",
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01"), [loungeFloor.Id]);
        var office = new SimulatedGroup(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Office",
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccc01"), [officeDesk.Id]);
        var bedroom = new SimulatedGroup(Guid.Parse("13131313-1313-1313-1313-131313131313"), "Bedroom",
            Guid.Parse("13131313-1313-1313-1313-131313131301"), [bedroomCeiling.Id]);
        var hallway = new SimulatedGroup(Guid.Parse("14141414-1414-1414-1414-141414141414"), "Hallway",
            Guid.Parse("14141414-1414-1414-1414-141414141401"), [hallwayCeiling.Id]);
        var garden = new SimulatedGroup(Guid.Parse("15151515-1515-1515-1515-151515151515"), "Garden",
            Guid.Parse("15151515-1515-1515-1515-151515151501"),
            [gardenPathLeft.Id, gardenPathRight.Id, gardenTerrace.Id, gardenSpotlight.Id, gardenShed.Id]);
        var downstairs = new SimulatedGroup(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "Downstairs",
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01"), [kitchenCeiling.Id, kitchenCounter.Id, loungeFloor.Id]);
        var upstairs = new SimulatedGroup(Guid.Parse("16161616-1616-1616-1616-161616161616"), "Upstairs",
            Guid.Parse("16161616-1616-1616-1616-161616161601"), [officeDesk.Id, bedroomCeiling.Id, hallwayCeiling.Id]);
        var eveningAreas = new SimulatedGroup(Guid.Parse("17171717-1717-1717-1717-171717171717"), "Evening areas",
            Guid.Parse("17171717-1717-1717-1717-171717171701"), [loungeFloor.Id, gardenTerrace.Id, gardenSpotlight.Id]);

        return new SimulatorSnapshot(
            [kitchenCeiling, kitchenCounter, loungeFloor, officeDesk, bedroomCeiling, hallwayCeiling, gardenPathLeft,
                gardenPathRight, gardenTerrace, gardenSpotlight, gardenShed],
            [kitchen, lounge, office, bedroom, hallway, garden],
            [downstairs, upstairs, eveningAreas],
            [],
            [new SimulatedScene(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), "Focus", [officeDesk.Id], 85,
                new HueColor(0.31, 0.32))]);
    }
}
