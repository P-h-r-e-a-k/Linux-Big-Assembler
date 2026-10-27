// The hidden bonus (LBA1 scene 96, actor 12, invisible): Twinsen in its zone (6) in discreet mode doing his action there -- LBA1 hid him
// with his discreet action, anim 15 -- and it gives its bonus, once.
void comportement_0()
{
    if (6 == zone_obj(0))
    {
        if (3 == comportement_hero())
        {
            oneif (1 == action())
            {
                set_track(label_0);
                set_comportement(comportement_1);
            }
        }
    }
}

void comportement_1()
{
    if (10 == l_track())
    {
        give_bonus(0);
        suicide();
    }
}
