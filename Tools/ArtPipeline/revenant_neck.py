"""Skin weights for the Brute's fitted throat, neck and lower face."""


def brute_neck_weights(point, weights, joints):
    x, height, forward = point
    if not (1.42 < height < 1.70 and abs(x) < .155):
        return weights
    existing = dict(weights)
    total = sum(existing.get(joint, 0.) for joint in joints)
    if total < .94:
        return weights  # Leave the shoulder and its arm weights alone.

    def band(low, high, value):
        t = max(0., min(1., (value - low) / (high - low)))
        return t * t * (3. - 2. * t)

    # Blend onto the chest near the clavicles. Bring the head weights lower
    # at the chin so the skin follows the rigid lips and teeth together.
    influence = (1. - band(.105, .155, abs(x))) * band(1.42, 1.47, height)
    neck = band(1.445, 1.55, height)
    head = band(1.53, 1.64, height + .073 * band(.035, .108, forward))
    targets = ((1. - neck) * (1. - head), neck * (1. - head), head)
    for joint, weight in zip(joints, targets):
        existing[joint] = (1. - influence) * existing.get(joint, 0.) + influence * total * weight
    return [(joint, weight) for joint, weight in existing.items() if weight > 0.]
