namespace CorgiAR
{
    public enum PetGrowthStage
    {
        Baby,
        Young,
        Adult
    }

    /// <summary>Pure growth-threshold rules shared by runtime code and EditMode tests.</summary>
    public static class PetGrowthMath
    {
        public static PetGrowthStage StageForChickenCount(
            int consumedChickenCount, int chickensForYoung, int additionalChickensForAdult)
        {
            int eaten = consumedChickenCount < 0 ? 0 : consumedChickenCount;
            int youngAt = chickensForYoung < 1 ? 1 : chickensForYoung;
            int adultAt = youngAt + (additionalChickensForAdult < 1 ? 1 : additionalChickensForAdult);

            if (eaten >= adultAt)
                return PetGrowthStage.Adult;
            if (eaten >= youngAt)
                return PetGrowthStage.Young;
            return PetGrowthStage.Baby;
        }

        public static int ChickenUntilNextStage(
            int consumedChickenCount, int chickensForYoung, int additionalChickensForAdult)
        {
            int eaten = consumedChickenCount < 0 ? 0 : consumedChickenCount;
            int youngAt = chickensForYoung < 1 ? 1 : chickensForYoung;
            int adultAt = youngAt + (additionalChickensForAdult < 1 ? 1 : additionalChickensForAdult);

            if (eaten < youngAt)
                return youngAt - eaten;
            if (eaten < adultAt)
                return adultAt - eaten;
            return 0;
        }
    }
}
