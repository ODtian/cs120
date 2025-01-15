import random

# 生成 10000 个随机的 0 和 1
random_bits = [str(random.randint(0, 1)) for _ in range(50000)]

# 将随机生成的 0 和 1 写入文件
with open("./input.txt", "w") as file:
    file.write("".join(random_bits))
