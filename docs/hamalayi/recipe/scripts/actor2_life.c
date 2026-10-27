// The ski instructor (LBA1 scene 96, actor 2): walks between the lift and the hut, turns to face Twinsen when he comes near, and when
// Twinsen speaks to him answers -- that he doesn't know where the plans are (in LBA1's chapters 13 and 14), and to use the red card on
// the ski lift.
void comportement_0()
{
    set_track(label_0);
    set_comportement(comportement_1);
}

void comportement_1()
{
    if (1300 > distance(0))
    {
        if (100 != l_track())
        {
            if (110 != l_track())
            {
                stop_l_track();
            }
        }
        set_track(label_100);
        set_comportement(comportement_2);
    }
    if (20 == life_point_obj(2))
    {
        if (2500 > distance(0))
        {
            oneif (4 == l_track())
            {
                give_bonus(0);
            }
        }
    }
}

void comportement_2()
{
    if (1400 < distance(0))
    {
        restore_l_track();
        set_comportement(comportement_1);
    }
}

void comportement_3()
{
    if (100 != l_track())
    {
        if (110 != l_track())
        {
            stop_l_track();
        }
    }
    set_track(label_100);
    set_comportement(comportement_4);
}

void comportement_4()
{
    if (110 == l_track())
    {
        if (13 == chapter() || 14 == chapter())
        {
            message(1003);
        }
        message(1002);
        set_comportement(comportement_2);
    }
}
