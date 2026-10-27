// The snowboard to pick up (LBA1 scene 96, actor 3): gone once Twinsen has it (game flag 200).
void comportement_0()
{
    if (1 == var_game(200))
    {
        suicide();
    }
    end_life();
}
