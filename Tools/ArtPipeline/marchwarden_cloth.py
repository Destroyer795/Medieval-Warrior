"""A separate, single-surface linen skirt suitable for live cloth simulation.

The belt pins two continuous side panels. The front/back splits permit a stride;
the cloth is anchored to the pelvis instead of being dragged by the thigh bones.
"""
for obj in list(groups['Body']):
    if obj.name.startswith(('Folded split linen tunic gore','Turned and stitched linen hem')):
        groups['Body'].remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
groups['Tunic']=[];current='Tunic';bone='Pelvis'
for side in range(2):
    def linen(u,v,side=side):
        # A small real opening at the front and back, continuous at the sides.
        a=side*math.pi+.024+u*(math.pi-.048)
        p=skirt_point(a,v)
        lip=.0015*math.exp(-((v-.985)/.02)**2)
        p+=Vector((math.sin(a)*lip,0,math.cos(a)*lip))
        return p
    obj=patch('Simulated split linen side panel',linen,28,22,2,0,0,(2,1.5))
    obj['region']='cloth'
print('MARCHWARDEN independent linen simulation: 2 side panels, pinned pelvis, open stride slits',flush=True)
