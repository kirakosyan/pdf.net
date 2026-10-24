# Independent analysis tools

The fidelity checker uses caller-provisioned pypdf, PyMuPDF/MuPDF, Pillow and
Poppler. None is referenced, bundled, downloaded or loaded by an SDK runtime
project. They run only in an explicitly provisioned development/qualification
environment. Reports record actual versions and executable hashes; retained tool
inventories bind supporting binaries and package metadata for the observed run.

The observed Windows source run used pypdf 6.10.0 (BSD-3-Clause), PyMuPDF 1.26.7
(AGPL-3.0/commercial dual licensing), Pillow 12.3.0 (HPND) and the caller's Poppler
analysis runtime (GPL). These are analysis tools, not a redistribution or license
grant for native SDK packages. Any future redistribution requires its own reviewed
license and complete notices. Original corpus PDFs and glyphs use the repository
MIT license and contain no borrowed font program.
