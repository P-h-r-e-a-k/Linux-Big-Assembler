// Twinsen in the ski resort (LBA1 scene 96, actor 0). LBA1's snowboard (inventory 13) and red card (8) have no LBA2 items: having the
// board is game flag 200, and using it -- or the card at the ski lift -- is the action key in the zones LBA1 used them in.
void comportement_0()
{
    if (0 == zone())
    {
        cam_follow(0);
        pos_point(3);
        beta(512);
    }
    set_comportement(comportement_1);
}

void comportement_1()
{
    swif (1 == action())
    {
        if (1250 > distance(2))
        {
            if (13 == chapter() || 14 == chapter())
            {
                message(1001);
            }
            if (13 != chapter())
            {
                if (14 != chapter())
                {
                    message(1000);
                }
            }
            set_comportement_obj(2, comportement_3);
        }
    }
    if (3 == col())
    {
        set_var_game(200, 1);
        kill_obj(3);
    }
    if (4 == zone() || 3 == zone() || 2 == zone() || 1 == zone())
    {
        if (1 == var_game(200))
        {
            if (1 == action())
            {
                set_dir(NO_MOVE);
                invisible(1);
                anim(0);
                comportement_hero(0);
                set_track(label_0);
                set_comportement(comportement_2);
                set_comportement_obj(13, comportement_1);
            }
        }
    }
    if (5 == zone())
    {
        if (1 == var_game(200))
        {
            if (1 == action())
            {
                set_dir(NO_MOVE);
                comportement_hero(0);
                set_track(label_4);
                set_comportement(comportement_4);
            }
        }
    }
}

void comportement_2()
{
}

void comportement_3()
{
    cam_follow(0);
    set_track_obj(13, label_1);
    set_dir(MOVE_MANUAL);
    invisible(0);
    set_comportement(comportement_1);
}

void comportement_4()
{
    if (100 == l_track())
    {
        set_var_game(201, 1);
        set_dir(MOVE_MANUAL);
        pos_point(3);
        beta(512);
        set_comportement(comportement_1);
    }
}
