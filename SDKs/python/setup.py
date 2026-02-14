import setuptools

with open("README.md", "r", encoding="utf-8") as fh:
    long_description = fh.read()

setuptools.setup(
    name="axardb-sdk",
    version="1.0.0",
    author="AxarDB",
    author_email="support@axardb.com",
    description="Python SDK for AxarDB",
    long_description=long_description,
    long_description_content_type="text/markdown",
    url="https://github.com/AxarDB/AxarDB",
    packages=setuptools.find_packages(),
    classifiers=[
        "Programming Language :: Python :: 3",
        "License :: OSI Approved :: MIT License",
        "Operating System :: OS Independent",
    ],
    python_requires='>=3.6',
    install_requires=[
        "requests>=2.25.1",
    ],
)
