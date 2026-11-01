using NVorbis.Contracts;

namespace NVorbis
{
    class Factory : IFactory
    {
        readonly SetupBudget budget = new SetupBudget();
        public IHuffman CreateHuffman()
        {
            return new Huffman();
        }

        public IMdct CreateMdct()
        {
            return new Mdct();
        }

        public ICodebook CreateCodebook()
        {
            return new Codebook { Budget = budget };
        }

        public IFloor CreateFloor(IPacket packet)
        {
            budget.Reserve(128 * 1024);
            var type = (int)packet.ReadBits(16);
            switch (type)
            {
                case 0: return new Floor0();
                case 1: return new Floor1();
                default: throw new System.IO.InvalidDataException("Invalid floor type!");
            }
        }

        public IMapping CreateMapping(IPacket packet)
        {
            budget.Reserve(128 * 1024);
            if (packet.ReadBits(16) != 0)
            {
                throw new System.IO.InvalidDataException("Invalid mapping type!");
            }

            return new Mapping();
        }

        public IMode CreateMode()
        {
            budget.Reserve(128 * 1024);
            return new Mode();
        }

        public IResidue CreateResidue(IPacket packet)
        {
            budget.Reserve(128 * 1024);
            var type = (int)packet.ReadBits(16);
            switch (type)
            {
                case 0: return new Residue0 { Budget = budget };
                case 1: return new Residue1 { Budget = budget };
                case 2: return new Residue2 { Budget = budget };
                default: throw new System.IO.InvalidDataException("Invalid residue type!");
            }
        }
    }
}
