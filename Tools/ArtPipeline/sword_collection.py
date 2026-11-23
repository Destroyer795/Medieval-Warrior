"""Original cosmetic swords. Run with Python to rebuild Unity assets, or Blender
in background mode with --python to rebuild the editable collection as well.
"""
import sys
from math import cos, pi, sin, tau
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sword_collection_geometry import Model, arc_points, mirror_guard

# sRGB display color, metallic response and smoothness for Standard / Principled.
FINISHES = {
    "PewterSteel": ("a5b2b7", .75, .57),
    "ColdSteel": ("a9bfca", .79, .69),
    "PolishedSilver": ("e6ebf2", .87, .89),
    "EtchedIron": ("52595b", .68, .45),
    "PatinatedBronze": ("94704e", .69, .49),
    "PaleBrass": ("d4ab65", .74, .66),
    "RoyalGold": ("f7c361", .82, .78),
    "Walnut": ("985939", .04, .34),
    "Oxblood": ("853a32", .04, .40),
    "WoadLeather": ("344f65", .03, .34),
    "WineLeather": ("642e43", .04, .40),
    "BlackCalf": ("383234", .03, .36),
    "Horn": ("856334", .06, .55),
    "BoneIvory": ("dfd0b1", .04, .52),
    "Sapphire": ("183f9b", .16, .86),
}

FULLER = [(-1,0),(-.66,.81),(-.33,1),(-.18,.53),(.18,.53),(.33,1),(.66,.81),(1,0)]
DIAMOND = [(-1,0),(-.57,.53),(0,1),(.57,.53),(1,0)]
SINGLE = [(-1,0),(-.92,.84),(-.62,1),(-.36,.70),(-.22,.87),(.28,.65),(1,0)]


def northmere():
    m = Model("NorthmereSword", "Northmere Sword",
              ("PewterSteel","PatinatedBronze","BlackCalf","PaleBrass"))
    m.blade([(-.279,0,.033,.006),(-.24,0,.035,.0065),(.10,0,.031,.0055),
             (.306,0,.024,.0045),(.382,0,.014,.003),(.43,0,0,0)], FULLER)
    m.grip([(-.419,0,.016,.013),(-.398,0,.018,.015),(-.35,0,.017,.015),
            (-.302,0,.019,.015),(-.284,0,.018,.014)], 8)
    guard = [(-.277,0,.015,.013),(-.277,.037,.013,.012),(-.279,.062,.011,.011),
             (-.286,.085,.009,.009),(-.288,.091,.006,.007)]
    m.guard("Boat-shaped guard", mirror_guard(guard))
    m.guard("Pommel bar", mirror_guard([(-.425,0,.0085,.014),
            (-.426,.029,.008,.0125),(-.429,.043,.006,.01),(-.427,.047,.003,.006)]))
    for i in range(-2,3):
        height = .021 - .0036*abs(i)
        m.bead("Lobed crown", 1, (-.429-height*.54, i*.0165,0),
               (height,.0108,.014-.0018*abs(i)),12,8)
        for sign in (-1,1):
            m.stroke("Lobe inlay",3,[(-.431,i*.0165,sign*(.013-.0018*abs(i))),
                  (-.435-height*.42,i*.0165,sign*(.0141-.0018*abs(i))),
                  (-.433-height*.77,i*.0165,sign*(.0115-.0018*abs(i)))],.0013,sides=6)
    for y in (-.065,-.048,-.031,.031,.048,.065):
        m.seal("Punched guard motif",3,-.278-abs(y)*.042,y,[(.009,.0035,.0045),(.012,.0027,.0033)],4)
    m.collar("Woven grip terminal",-.413,.008,.018,.015,3)
    m.collar("Woven grip terminal",-.294,.008,.0195,.016,3)
    return m


def falchion():
    m = Model("FenlandFalchion","Fenland Falchion",
              ("PewterSteel","EtchedIron","Walnut","PaleBrass"))
    m.blade([(-.277,0,.018,.0065),(-.19,-.001,.023,.006),(.038,-.005,.031,.0063),
             (.205,-.011,.041,.006),(.299,-.013,.048,.0056),(.337,-.007,.043,.0048),
             (.380,.002,.027,.0035),(.43,0,0,0)], SINGLE)
    m.grip([(-.422,.001,.02,.013),(-.4,-.001,.017,.015),(-.363,-.003,.0155,.0145),
            (-.324,-.002,.018,.0155),(-.287,0,.02,.0145)])
    m.guard("Asymmetric beaked cross", [(-.258,-.09,.005,.005),(-.252,-.087,.008,.008),
            (-.253,-.076,.009,.009),(-.267,-.057,.007,.008),(-.276,-.025,.009,.011),
            (-.277,.005,.015,.014),(-.285,.045,.009,.01),(-.301,.072,.007,.008),
            (-.317,.079,.006,.007),(-.319,.084,.008,.007)])
    m.guard("Peaked iron pommel", [(-.429,-.023,.009,.011),(-.437,-.010,.016,.015),
            (-.438,.007,.019,.016),(-.434,.026,.012,.014),(-.423,.030,.006,.009)])
    m.collar("Forward ferrule",-.29,.012,.022,.0155)
    m.collar("Rear ferrule",-.416,.009,.021,.0145)
    for x,y in [(-.393,-.002),(-.355,-.003),(-.317,-.002)]:
        m.seal("Pinned scales",3,x,y,[(.0135,.0045,.0045),(.0161,.0038,.0038)],10)
    m.seal("Pommel rosette",3,-.438,.002,[(.014,.008,.008),(.0168,.0058,.0058)],8,pi/8)
    return m


def estoc():
    m = Model("BriarEstoc","Briar Estoc",("ColdSteel","EtchedIron","Oxblood","PaleBrass"))
    m.blade([(-.277,0,.019,.009),(-.239,0,.019,.009),(-.208,0,.013,.008),
             (.083,0,.0105,.007),(.336,0,.0048,.0038),(.43,0,0,0)], DIAMOND)
    m.grip([(-.42,0,.014,.012),(-.40,0,.0145,.013),(-.361,0,.018,.015),
            (-.319,0,.016,.014),(-.285,0,.014,.0125)],8,3,-1)
    m.guard("S-shaped quillons",[(-.303,-.106,.004,.005),(-.305,-.098,.007,.007),
            (-.297,-.088,.006,.006),(-.281,-.066,.0055,.006),(-.270,-.039,.006,.007),
            (-.277,0,.013,.012),(-.282,.035,.007,.007),(-.269,.065,.0055,.006),
            (-.253,.084,.006,.006),(-.251,.097,.0065,.007),(-.257,.102,.004,.005)])
    for sy in (-1,1):
        sz = sy
        p0,p1,p2,p3 = (-.278,sy*.035,0),(-.202,sy*.070,sz*.028),(-.192,sy*.014,sz*.031),(-.221,sy*.010,0)
        m.stroke("Briar finger ring",1,arc_points(p0,p1,p2,p3,16),.0039,.0048)
        m.bead("Quillon gilding",3,(-.303 if sy<0 else -.255,sy*.102,0),(.006,.005,.006))
    for x in (-.413,-.292): m.collar("Wire ferrule",x,.009,.0168,.0145,3)
    m.lathe("Fluted scent-stopper pommel",1,[(-.463,0,0,.004,.004),(-.454,0,0,.012,.011),
            (-.439,0,0,.023,.0185),(-.428,0,0,.019,.016),(-.419,0,0,.011,.010)],8,15)
    m.collar("Gilt pommel button",-.459,.006,.008,.0075,3)
    m.collar("Pommel collar",-.422,.006,.016,.014,3)
    return m


def broadsword():
    m = Model("SaltireBroadsword","Saltire Broadsword",
              ("ColdSteel","EtchedIron","WoadLeather","PaleBrass"))
    m.blade([(-.279,0,.027,.0065),(-.214,0,.029,.0063),(.10,0,.025,.0052),
             (.306,0,.019,.0042),(.381,0,.010,.0033),(.43,0,0,0)], FULLER)
    m.grip([(-.419,0,.014,.013),(-.38,0,.018,.015),(-.34,0,.017,.015),
            (-.29,0,.017,.014)],7)
    m.guard("Basket cross",[(-.297,-.058,.006,.007),(-.29,-.053,.008,.008),
            (-.277,-.033,.011,.011),(-.278,0,.014,.014),(-.283,.039,.01,.012),
            (-.300,.069,.008,.010),(-.306,.078,.006,.008)])
    m.lathe("Polygonal bun pommel",1,[(-.458,0,0,.006,.006),(-.451,0,0,.014,.012),
            (-.441,0,0,.023,.017),(-.428,0,0,.024,.018),
            (-.417,0,0,.012,.012)],12,28)
    for side in (-1,1):
        # Open basket around the knuckle side of the hand. Paired ribs and a
        # pierced saltire stay visible from front, back and the blade spine.
        def ribbon(name, p, width=.0038, depth=.0028):
            m.stroke(name,1,arc_points(*[(x,y,side*z) for x,y,z in p],12),width,depth)
        ribbon("Outer knuckle bow",[(-.432,.014,.005),(-.418,.081,.021),
                (-.356,.091,.020),(-.300,.070,.005)],.0055,.004)
        ribbon("Basket saltire A",[(-.4,.057,.021),(-.363,.040,.033),
                (-.32,.039,.034),(-.281,.018,.008)],.0046,.003)
        ribbon("Basket saltire B",[(-.416,.027,.012),(-.373,.036,.030),
                (-.341,.058,.034),(-.304,.071,.007)],.0046,.003)
        ribbon("Rear guard branch",[(-.432,.006,.005),(-.405,-.025,.021),
                (-.325,-.033,.029),(-.279,-.032,.009)],.0045,.0032)
        ribbon("Basket lower rail",[(-.414,.026,.013),(-.372,.025,.031),
                (-.304,.004,.027),(-.279,-.032,.009)],.0036,.003)
        m.bead("Crossed bars rivet",3,(-.361,.044,side*.030),(.0045,.0045,.0033))
    m.collar("Rear wire band",-.414,.009,.0165,.014,3)
    m.collar("Guard wire band",-.293,.009,.0185,.015,3)
    m.collar("Pommel finial",-.453,.006,.012,.01,3)
    return m


def kilij():
    m = Model("SaffronKilij","Saffron Kilij",("PewterSteel","PaleBrass","Horn","Oxblood"))
    m.blade([(-.276,0,.020,.0064),(-.199,-.016,.022,.0062),(-.024,-.039,.023,.006),
             (.134,-.043,.021,.0054),(.198,-.040,.020,.0054),(.209,-.044,.0245,.0055),
             (.282,-.038,.027,.0054),(.344,-.026,.022,.0046),(.398,-.010,.011,.003),
             (.43,0,0,0)], SINGLE)
    m.grip([(-.423,.009,.021,.014),(-.404,.006,.019,.014),(-.377,.001,.016,.014),
            (-.352,0,.0165,.015),(-.319,.001,.018,.015),(-.286,0,.017,.014)])
    m.guard("Ottoman lobed cross",mirror_guard([(-.276,0,.016,.0135),
            (-.275,.028,.011,.011),(-.273,.052,.006,.008),(-.267,.079,.0045,.006),
            (-.267,.087,.008,.008),(-.27,.093,.004,.0045)]))
    m.lathe("Hooked horn pommel",2,[(-.458,.029,0,.0025,.003),(-.452,.026,0,.009,.008),
            (-.442,.018,0,.017,.013),(-.432,.012,0,.023,.0155),(-.414,.007,0,.019,.015)],14,36)
    for sign in (-1,1):
        m.stroke("Pointed langet",1,[(-.312,0,sign*.015),(-.291,0,sign*.014),
                (-.275,0,sign*.0128),(-.258,0,sign*.008),(-.227,0,sign*.0063)],
                [.0016,.007,.011,.007,.0013],[.0018,.0028,.003,.0026,.0014])
    m.stroke("Grip spine",1,arc_points((-.447,.031,0),(-.422,.035,.001),
            (-.382,.014,.001),(-.287,.018,.001),13),.0017,.003)
    for x,y in [(-.399,.004),(-.369,.0005),(-.337,0)]:
        m.seal("Grip pin head",1,x,y,[(.0138,.0038,.0038),(.0158,.003,.003)],10)
    m.seal("Pommel red enamel",3,-.433,.013,[(.014,.0072,.0072),(.016,.0055,.0055)],10)
    m.seal("Cross red enamel",3,-.276,0,[(.0154,.006,.006),(.017,.0048,.0048)],8)
    return m


def cinquedea():
    m = Model("IvoryCinquedea","Ivory Cinquedea",
              ("PewterSteel","PatinatedBronze","BoneIvory","PaleBrass"))
    # Three longitudinal channels divide the broad forte into distinct planes.
    triple = [(-1,0),(-.86,.71),(-.72,1),(-.62,.57),(-.50,.57),(-.39,.98),
              (-.17,1),(-.08,.58),(.08,.58),(.17,1),(.39,.98),(.50,.57),
              (.62,.57),(.72,1),(.86,.71),(1,0)]
    m.blade([(-.278,0,.059,.0066),(-.242,0,.061,.007),(-.120,0,.052,.0066),
             (.072,0,.035,.0058),(.25,0,.019,.0046),(.367,0,.008,.003),(.43,0,0,0)],triple)
    m.grip([(-.422,0,.027,.013),(-.400,0,.023,.014),(-.374,0,.018,.0145),
            (-.349,0,.017,.015),(-.321,0,.021,.015),(-.288,0,.029,.014)])
    m.guard("Arched bronze guard",mirror_guard([(-.277,0,.015,.015),(-.275,.035,.012,.014),
            (-.265,.057,.009,.011),(-.248,.074,.007,.009),(-.229,.085,.004,.006)]))
    m.guard("Fan pommel",mirror_guard([(-.437,0,.013,.015),(-.434,.018,.010,.014),
            (-.427,.029,.008,.010),(-.420,.036,.005,.007)]))
    for x in (-.398,-.352,-.308):
        m.seal("Venetian rosette",1,x,0,[(.0146,.006,.006),(.0159,.0048,.0048)],8)
        m.seal("Rosette pin",3,x,0,[(.0159,.0032,.0032),(.0172,.0024,.0024)],8,pi/8)
    for sign in (-1,1):
        for j in (-1,0,1):
            m.stroke("Guard flute",3,[(-.277+j*.0035,.022*sign,.0141),
                    (-.270+j*.0035,.044*sign,.0134),(-.258+j*.0033,.061*sign,.0102)],
                    .0012,sides=6)
            m.stroke("Reflected guard flute",3,[(-.277+j*.0035,.022*sign,-.0141),
                    (-.270+j*.0035,.044*sign,-.0134),(-.258+j*.0033,.061*sign,-.0102)],
                    .0012,sides=6)
    return m


def kings_sword():
    m = Model("KingsSword","King's Sword",("PolishedSilver","RoyalGold","WineLeather","Sapphire"))
    m.blade([(-.277,0,.030,.0073),(-.243,0,.033,.0073),(-.193,0,.035,.007),
             (-.171,0,.03,.0066),(.135,0,.023,.006),(.315,0,.016,.0048),
             (.379,0,.009,.003),(.43,0,0,0)],FULLER)
    m.grip([(-.417,0,.017,.0135),(-.397,0,.016,.014),(-.352,0,.019,.016),
            (-.304,0,.016,.014),(-.286,0,.019,.015)],7,1)
    m.guard("Coronation cross",mirror_guard([(-.276,0,.017,.0145),(-.274,.033,.014,.013),
            (-.266,.061,.010,.011),(-.253,.085,.007,.009),
            (-.244,.103,.006,.007),(-.233,.111,.004,.005)]))
    for sy in (-1,1):
        # Openwork trefoil terminals. Negative space separates the three petals.
        for top in (-1,1):
            points=arc_points((-.252,sy*.086,0),(-.251+top*.028,sy*.081,.001),
                    (-.249+top*.023,sy*.114,.001),(-.235,sy*.107,0),12)
            m.stroke("Fleuron outer petal",1,points,.0032,.0043)
        m.bead("Fleuron spear tip",1,(-.232,sy*.109,0),(.009,.005,.006),8,6,18)
        for sz in (-1,1):
            points=arc_points((-.279,sy*.014,sz*.0126),(-.314,sy*.038,sz*.010),
                    (-.256,sy*.047,sz*.010),(-.260,sy*.071,sz*.008),12)
            m.stroke("Pierced vine scroll",1,points,.0028,.0032)
            m.stroke("Gilt blade tongue",1,[(-.277,sy*.019,sz*.013),(-.255,sy*.018,sz*.008),
                    (-.238,sy*.011,sz*.0068),(-.212,sy*.0055,sz*.0069)],
                    [.006,.004,.0032,.0008],[.003,.002,.0017,.0011])
    for x in (-.412,-.294):
        m.collar("Beaded ferrule",x,.012,.019,.0165)
        for i in range(12):
            angle=tau*i/12
            m.bead("Ferrule seed",1,(x,.019*cos(angle),.0164*sin(angle)),(.0025,.0025,.0025),7,4)
    m.seal("Jeweled cross bezel",1,-.277,0,[(.009,.020,.023),(.014,.020,.023),(.017,.014,.015)],8,pi/8)
    m.seal("Faceted blue cross stone",3,-.277,0,[(.0165,.012,.013),(.0205,.007,.008)],8,pi/8)
    m.seal("Wheel pommel",1,-.439,0,[(.0001,.027,.030),(.009,.027,.030),
             (.014,.023,.026),(.0155,.019,.022)],20,pi/20)
    # A double-sided setting below the palm remains visible in either guard.
    m.seal("Burnished pommel bezel",1,-.439,0,[(.0152,.020,.023),(.017,.019,.022)],12,pi/12)
    m.seal("Pommel sapphire",3,-.439,0,[(.0168,.0172,.0197),(.0185,.0172,.0197),
            (.022,.0112,.0134),(.0237,.0051,.0063)],10,pi/10)
    for sz in (-1,1):
        for i in range(4):
            angle=pi/4+tau*i/4
            m.bead("Rounded gold claw",1,(-.439+.0167*cos(angle),.0194*sin(angle),sz*.0185),
                   (.0025,.0025,.0022),8,5)
    return m


def collection():
    return [northmere(),falchion(),estoc(),broadsword(),kilij(),cinquedea(),kings_sword()]


if __name__ == "__main__":
    from sword_collection_export import export
    export(collection(), FINISHES)
