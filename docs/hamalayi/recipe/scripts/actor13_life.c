// Twinsen on his snowboard (LBA1 scene 96, actor 13; the map's own character): hidden until Twinsen uses the board in one of the four
// start zones, then rides that zone's run down the mountain (tracks 3 to 6), the camera on him; at the bottom (label 111) Twinsen stands
// there again and this one hides. Over the jumps (labels 50, 51) he doesn't fall.
void comportement_0()
{
}

void comportement_1()
{
    body(0);
    anim(0);
    if (1 == zone_obj(0))
    {
        pos_point(4);
        beta(0);
        set_track(label_3);
    }
    else if (2 == zone_obj(0))
    {
        pos_point(20);
        beta(1024);
        set_track(label_4);
    }
    else if (3 == zone_obj(0))
    {
        pos_point(27);
        beta(0);
        set_track(label_5);
    }
    else if (4 == zone_obj(0))
    {
        pos_point(33);
        beta(0);
        set_track(label_6);
    }
    cam_follow(13);
    set_comportement(comportement_2);
}

void comportement_2()
{
    swif (111 == l_track())
    {
        set_track_obj(0, label_2);
        set_comportement(comportement_0);
        set_comportement_obj(0, comportement_3);
    }
    swif (50 == l_track())
    {
        fallable(0);
    }
    swif (51 == l_track())
    {
        fallable(1);
    }
}
